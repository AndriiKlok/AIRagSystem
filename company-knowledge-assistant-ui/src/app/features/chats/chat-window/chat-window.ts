import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, ActivatedRoute } from '@angular/router';
import { ApiService, Message } from '../../../core/services/api.service';
import { SignalrService } from '../../../core/services/signalr.service';
import { SafeHtmlPipe } from '../../../shared/pipes/safe-html.pipe';
import { Inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

@Component({
  selector: 'app-chat-window',
  imports: [CommonModule, FormsModule, RouterModule, SafeHtmlPipe],
  templateUrl: './chat-window.html',
  styleUrl: './chat-window.css',
})
export class ChatWindow implements OnInit, OnDestroy {
  chatId: number = 0;
  messages: Message[] = [];
  newMessage = '';
  isLoading = false;

  constructor(
    private route: ActivatedRoute,
    private apiService: ApiService,
    private signalrService: SignalrService,
    @Inject(PLATFORM_ID) private platformId: Object
  ) { }

  ngOnInit(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    this.chatId = Number(this.route.snapshot.paramMap.get('id'));
    if (this.chatId) {
      this.loadMessages();
      this.signalrService.startConnection();
      this.signalrService.joinChat(this.chatId);
      this.signalrService.addMessageListener((message: Message) => {
        this.messages.push(message);
      });
    }
  }

  ngOnDestroy(): void {
    if (isPlatformBrowser(this.platformId)) {
      this.signalrService.leaveChat(this.chatId);
      this.signalrService.removeMessageListener();
      this.signalrService.stopConnection();
    }
  }

  loadMessages(): void {
    this.apiService.getMessages(this.chatId).subscribe({
      next: (messages) => this.messages = messages,
      error: (err) => console.error('Error loading messages:', err)
    });
  }

  sendMessage(): void {
    if (!this.newMessage.trim()) return;

    this.isLoading = true;
    this.apiService.sendMessage(this.chatId, this.newMessage).subscribe({
      next: (response) => {
        // Message will be received via SignalR
        this.newMessage = '';
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Error sending message:', err);
        this.isLoading = false;
      }
    });
  }

  onKeyPress(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.sendMessage();
    }
  }

  parseSources(sources: string): any[] {
    try {
      return JSON.parse(sources);
    } catch {
      return [];
    }
  }
}