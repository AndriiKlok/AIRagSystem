import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';

@Component({
  selector: 'app-document-upload',
  imports: [CommonModule, RouterModule],
  templateUrl: './document-upload.html',
  styleUrl: './document-upload.css',
})
export class DocumentUpload {
  areaId: number = 0;
  selectedFile: File | null = null;
  isUploading = false;
  uploadProgress = 0;

  constructor(
    private route: ActivatedRoute,
    private apiService: ApiService,
    private router: Router
  ) {
    this.areaId = Number(this.route.snapshot.queryParamMap.get('areaId')) || 0;
  }

  onFileSelected(event: any): void {
    this.selectedFile = event.target.files[0];
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    const files = event.dataTransfer?.files;
    if (files && files.length > 0) {
      this.selectedFile = files[0];
    }
  }

  uploadDocument(): void {
    if (!this.selectedFile || !this.areaId) return;

    this.isUploading = true;
    this.uploadProgress = 0;

    this.apiService.uploadDocument(this.areaId, this.selectedFile).subscribe({
      next: () => {
        this.isUploading = false;
        alert('Document uploaded successfully!');
        this.selectedFile = null;
        this.router.navigate(['/areas', this.areaId]);
      },
      error: (err) => {
        this.isUploading = false;
        console.error('Error uploading document:', err);
        alert('Error uploading document');
      }
    });
  }
}