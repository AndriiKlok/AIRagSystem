import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { ApiService, Area, Document, Chat } from '../../../core/services/api.service';
import { SignalrService } from '../../../core/services/signalr.service';
import { Inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

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
  processingDocuments: { [key: number]: { status: string; progress: number; error?: string } } = {};
  private lastProgressUpdateAt: { [key: number]: number } = {};
  private processingPollInterval: ReturnType<typeof setInterval> | null = null;
  private readonly pollIntervalMs = 5000;
  private readonly staleThresholdMs = 120000;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private apiService: ApiService,
    private signalrService: SignalrService,
    @Inject(PLATFORM_ID) private platformId: Object
  ) { }

  ngOnInit(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    this.areaId = Number(this.route.snapshot.paramMap.get('id'));
    if (this.areaId) {
      this.loadArea();
      this.loadDocuments();
      this.loadChats();
      this.initializeSignalR();
    }
  }

  ngOnDestroy(): void {
    if (isPlatformBrowser(this.platformId)) {
      this.signalrService.leaveArea(this.areaId);
      if (this.processingPollInterval) {
        clearInterval(this.processingPollInterval);
        this.processingPollInterval = null;
      }
    }
  }

  private initializeSignalR(): void {
    this.signalrService.startConnection();
    this.signalrService.joinArea(this.areaId);
    this.signalrService.addDocumentProgressListener((progress: any) => {
      this.processingDocuments[progress.documentId] = {
        status: progress.status,
        progress: progress.progress,
        error: progress.error
      };
      this.lastProgressUpdateAt[progress.documentId] = Date.now();

      // Reload documents when processing is complete
      if (progress.status === 'Completed' || progress.status === 'Failed') {
        setTimeout(() => this.loadDocuments(), 1000);
      }

      this.updateProcessingPollingState();
    });
  }

  loadArea(): void {
    this.apiService.getArea(this.areaId).subscribe({
      next: (area) => this.area = area,
      error: (err) => console.error('Error loading area:', err)
    });
  }

  loadDocuments(): void {
    this.apiService.getDocuments(this.areaId).subscribe({
      next: (documents) => {
        this.documents = documents;
        // Clear completed processing status
        documents.forEach(doc => {
          if (doc.processingStatus === 'Completed' || doc.processingStatus === 'Failed') {
            delete this.processingDocuments[doc.id];
            delete this.lastProgressUpdateAt[doc.id];
          }
        });
        this.updateProcessingPollingState();
      },
      error: (err) => console.error('Error loading documents:', err)
    });
  }

  private updateProcessingPollingState(): void {
    const hasProcessingDocuments = this.documents.some(doc => this.isDocumentProcessing(doc));

    if (hasProcessingDocuments && !this.processingPollInterval) {
      this.processingPollInterval = setInterval(() => this.loadDocuments(), this.pollIntervalMs);
      return;
    }

    if (!hasProcessingDocuments && this.processingPollInterval) {
      clearInterval(this.processingPollInterval);
      this.processingPollInterval = null;
    }
  }

  loadChats(): void {
    this.apiService.getChats(this.areaId).subscribe({
      next: (chats) => this.chats = chats,
      error: (err) => console.error('Error loading chats:', err)
    });
  }

  createChat(): void {
    if (!this.canCreateChat) {
      alert('Wait until all uploaded documents are fully processed before starting a chat.');
      return;
    }

    const chatName = prompt('Enter chat name:');
    if (chatName) {
      this.apiService.createChat({ areaId: this.areaId, name: chatName }).subscribe({
        next: (chat) => {
          this.chats.push(chat);
          this.router.navigate(['/chats', chat.id]);
        },
        error: (err) => console.error('Error creating chat:', err)
      });
    }
  }

  deleteDocument(id: number): void {
    if (confirm('Are you sure you want to delete this document?')) {
      this.apiService.deleteDocument(id).subscribe({
        next: () => this.loadDocuments(),
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

  getDocumentStatus(document: Document): { status: string; progress: number; error?: string } {
    return this.processingDocuments[document.id] || {
      status: document.processingStatus,
      progress: document.processingStatus === 'Completed' ? 100 : 0,
      error: document.errorMessage
    };
  }

  isDocumentProcessing(document: Document): boolean {
    const status = this.getDocumentStatus(document);
    return status.status === 'Processing' || status.status === 'Uploading';
  }

  isDocumentProgressStale(document: Document): boolean {
    if (!this.isDocumentProcessing(document)) {
      return false;
    }

    const lastProgressAt = this.lastProgressUpdateAt[document.id];
    if (lastProgressAt) {
      return Date.now() - lastProgressAt > this.staleThresholdMs;
    }

    const uploadedAt = Date.parse(document.uploadedAt);
    if (Number.isNaN(uploadedAt)) {
      return false;
    }

    return Date.now() - uploadedAt > this.staleThresholdMs;
  }

  getDocumentStaleSeconds(document: Document): number {
    const lastProgressAt = this.lastProgressUpdateAt[document.id] || Date.parse(document.uploadedAt);
    if (!lastProgressAt || Number.isNaN(lastProgressAt)) {
      return 0;
    }

    return Math.floor((Date.now() - lastProgressAt) / 1000);
  }

  get canCreateChat(): boolean {
    if (this.documents.length === 0) {
      return false;
    }

    return this.documents.every(doc => this.getDocumentStatus(doc).status === 'Completed');
  }
}
