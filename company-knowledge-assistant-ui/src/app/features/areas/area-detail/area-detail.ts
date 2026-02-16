import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { ApiService, Area, Document, Chat } from '../../../core/services/api.service';

@Component({
  selector: 'app-area-detail',
  imports: [CommonModule, RouterModule],
  templateUrl: './area-detail.html',
  styleUrl: './area-detail.css',
})
export class AreaDetail implements OnInit {
  area: Area | null = null;
  documents: Document[] = [];
  chats: Chat[] = [];
  areaId: number = 0;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private apiService: ApiService
  ) { }

  ngOnInit(): void {
    this.areaId = Number(this.route.snapshot.paramMap.get('id'));
    if (this.areaId) {
      this.loadArea();
      this.loadDocuments();
      this.loadChats();
    }
  }

  loadArea(): void {
    this.apiService.getArea(this.areaId).subscribe({
      next: (area) => this.area = area,
      error: (err) => console.error('Error loading area:', err)
    });
  }

  loadDocuments(): void {
    this.apiService.getDocuments(this.areaId).subscribe({
      next: (documents) => this.documents = documents,
      error: (err) => console.error('Error loading documents:', err)
    });
  }

  loadChats(): void {
    this.apiService.getChats(this.areaId).subscribe({
      next: (chats) => this.chats = chats,
      error: (err) => console.error('Error loading chats:', err)
    });
  }

  createChat(): void {
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
}
