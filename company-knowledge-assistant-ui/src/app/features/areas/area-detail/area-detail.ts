import { Component, OnInit, OnDestroy, Inject, PLATFORM_ID, NgZone, ChangeDetectorRef } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { HttpEventType } from '@angular/common/http';
import { ApiService, Area, Document, Chat } from '../../../core/services/api.service';
import { SignalrService } from '../../../core/services/signalr.service';

export interface PendingUpload {
  file: File;
  progress: number;
  status: 'uploading' | 'done' | 'error';
  error?: string;
}

@Component({
  selector: 'app-area-detail',
  imports: [CommonModule, RouterModule],
  templateUrl: './area-detail.html',
  styleUrl: './area-detail.css',
})
export class AreaDetail implements OnInit, OnDestroy {
  area: Area | null = null;
  documents: Document[] = [];
  chats: Chat[] = [];
  areaId: number = 0;

  // Inline drag-and-drop upload
  isDragOver = false;
  pendingUploads: PendingUpload[] = [];

  // Per-document analysis state (from SignalR + local optimistic updates)
  analysisProgress: { [key: number]: { status: string; progress: number; error?: string } } = {};
  private lastProgressAt: { [key: number]: number } = {};
  private pollInterval: ReturnType<typeof setInterval> | null = null;
  private readonly pollMs = 5000;
  private readonly staleMs = 120000;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private apiService: ApiService,
    private signalrService: SignalrService,
    private ngZone: NgZone,
    private cdr: ChangeDetectorRef,
    @Inject(PLATFORM_ID) private platformId: Object
  ) { }

  ngOnInit(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    this.areaId = Number(this.route.snapshot.paramMap.get('id'));
    if (this.areaId) {
      this.loadArea();
      this.loadDocuments();
      this.loadChats();
      this.initSignalR();
    }
  }

  ngOnDestroy(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    this.signalrService.leaveArea(this.areaId);
    if (this.pollInterval) clearInterval(this.pollInterval);
  }

  // ── SignalR ───────────────────────────────────────────────────────────────

  private initSignalR(): void {
    this.signalrService.startConnection();
    this.signalrService.joinArea(this.areaId);
    this.signalrService.addDocumentProgressListener((progress: any) => {
      this.ngZone.run(() => {
        this.analysisProgress[progress.documentId] = {
          status: progress.status,
          progress: progress.progress,
          error: progress.error
        };
        this.lastProgressAt[progress.documentId] = Date.now();

        if (['Completed', 'Failed', 'Uploaded'].includes(progress.status)) {
          setTimeout(() => this.loadDocuments(), 800);
        }
        this.syncPollState();
        this.cdr.detectChanges();
      });
    });
  }

  // ── Data loading ──────────────────────────────────────────────────────────

  loadArea(): void {
    this.apiService.getArea(this.areaId).subscribe({
      next: (area) => this.ngZone.run(() => { this.area = area; this.cdr.detectChanges(); }),
      error: (err) => console.error('Error loading area:', err)
    });
  }

  loadDocuments(): void {
    this.apiService.getDocuments(this.areaId).subscribe({
      next: (docs) => this.ngZone.run(() => {
        this.documents = [...docs];
        docs.forEach(doc => {
          if (['Completed', 'Failed', 'Uploaded'].includes(doc.processingStatus)) {
            delete this.analysisProgress[doc.id];
            delete this.lastProgressAt[doc.id];
          }
        });
        this.syncPollState();
        this.cdr.detectChanges();
      }),
      error: (err) => console.error('Error loading documents:', err)
    });
  }

  loadChats(): void {
    this.apiService.getChats(this.areaId).subscribe({
      next: (chats) => this.ngZone.run(() => { this.chats = [...chats]; this.cdr.detectChanges(); }),
      error: (err) => console.error('Error loading chats:', err)
    });
  }

  private syncPollState(): void {
    const busy = this.documents.some(d => this.isAnalyzing(d));
    if (busy && !this.pollInterval) {
      this.pollInterval = setInterval(() => this.loadDocuments(), this.pollMs);
    } else if (!busy && this.pollInterval) {
      clearInterval(this.pollInterval);
      this.pollInterval = null;
    }
  }

  // ── Inline upload ─────────────────────────────────────────────────────────

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver = true;
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver = false;
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver = false;
    if (event.dataTransfer?.files) this.uploadFiles(event.dataTransfer.files);
  }

  openFilePicker(): void {
    (window.document.getElementById('areaFileInput') as HTMLInputElement)?.click();
  }

  onFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files) this.uploadFiles(input.files);
    input.value = '';
  }

  uploadFiles(files: FileList): void {
    Array.from(files).forEach(file => {
      const pending: PendingUpload = { file, progress: 0, status: 'uploading' };
      this.pendingUploads = [...this.pendingUploads, pending];

      this.apiService.uploadDocumentWithProgress(this.areaId, file).subscribe({
        next: (event) => this.ngZone.run(() => {
          if (event.type === HttpEventType.UploadProgress && event.total) {
            pending.progress = Math.round(100 * event.loaded / event.total);
          } else if (event.type === HttpEventType.Response) {
            pending.progress = 100;
            pending.status = 'done';
            setTimeout(() => {
              this.pendingUploads = this.pendingUploads.filter(p => p !== pending);
              this.loadDocuments();
              this.cdr.detectChanges();
            }, 1200);
          }
          this.cdr.detectChanges();
        }),
        error: (err) => this.ngZone.run(() => {
          pending.status = 'error';
          pending.error = err.message || 'Upload failed';
          this.cdr.detectChanges();
        })
      });
    });
  }

  // ── Analysis ──────────────────────────────────────────────────────────────

  analyzeDocument(id: number): void {
    this.analysisProgress[id] = { status: 'Processing', progress: 0 };
    this.lastProgressAt[id] = Date.now();   // start stale timer from NOW, not from uploadedAt
    this.cdr.detectChanges();
    this.syncPollState();

    this.apiService.analyzeDocument(id).subscribe({
      next: () => {
        this.analysisProgress[id] = { status: 'Processing', progress: 5 };
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.analysisProgress[id] = { status: 'Failed', progress: 0, error: err.message };
        this.cdr.detectChanges();
      }
    });
  }

  // ── Status helpers ────────────────────────────────────────────────────────

  getAnalysisStatus(doc: Document): { status: string; progress: number; error?: string } {
    return this.analysisProgress[doc.id] ?? {
      status: doc.processingStatus,
      progress: doc.processingStatus === 'Completed' ? 100 : 0,
      error: doc.errorMessage
    };
  }

  isAnalyzing(doc: Document): boolean {
    return this.getAnalysisStatus(doc).status === 'Processing';
  }

  isProgressStale(doc: Document): boolean {
    if (!this.isAnalyzing(doc)) return false;
    const last = this.lastProgressAt[doc.id];
    if (last) return Date.now() - last > this.staleMs;
    const uploaded = Date.parse(doc.uploadedAt);
    return !isNaN(uploaded) && Date.now() - uploaded > this.staleMs;
  }

  getStaleSeconds(doc: Document): number {
    const last = this.lastProgressAt[doc.id] ?? Date.parse(doc.uploadedAt);
    return last && !isNaN(last) ? Math.floor((Date.now() - last) / 1000) : 0;
  }

  get canCreateChat(): boolean {
    return this.documents.some(d => d.processingStatus === 'Completed');
  }

  // ── Chat ──────────────────────────────────────────────────────────────────

  createChat(): void {
    if (!this.canCreateChat) {
      alert('Analyze at least one document before starting a chat.');
      return;
    }
    const chatName = prompt('Enter chat name:');
    if (chatName) {
      this.apiService.createChat({ areaId: this.areaId, name: chatName }).subscribe({
        next: (chat) => this.ngZone.run(() => {
          this.chats = [...this.chats, chat];
          this.router.navigate(['/chats', chat.id]);
        }),
        error: (err) => console.error('Error creating chat:', err)
      });
    }
  }

  deleteDocument(id: number): void {
    if (confirm('Are you sure you want to delete this document?')) {
      this.apiService.deleteDocument(id).subscribe({
        next: () => this.ngZone.run(() => {
          this.documents = this.documents.filter(d => d.id !== id);
          delete this.analysisProgress[id];
          this.cdr.detectChanges();
        }),
        error: (err) => console.error('Error deleting document:', err)
      });
    }
  }

  deleteChat(id: number): void {
    if (confirm('Are you sure you want to delete this chat?')) {
      this.apiService.deleteChat(id).subscribe({
        next: () => this.loadChats(),
        error: (err) => console.error('Error deleting chat:', err)
      });
    }
  }
}
