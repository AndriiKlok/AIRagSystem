import { Component, OnInit, OnDestroy, AfterViewChecked, ViewChild, ElementRef, NgZone, Inject, PLATFORM_ID } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, ActivatedRoute } from '@angular/router';
import { ApiService, Message } from '../../../core/services/api.service';
import { SignalrService } from '../../../core/services/signalr.service';
import { SafeHtmlPipe } from '../../../shared/pipes/safe-html.pipe';

@Component({
  selector: 'app-chat-window',
  imports: [CommonModule, FormsModule, RouterModule, SafeHtmlPipe],
  templateUrl: './chat-window.html',
  styleUrl: './chat-window.css',
})
export class ChatWindow implements OnInit, OnDestroy, AfterViewChecked {
  @ViewChild('messagesContainer') private messagesContainer!: ElementRef;

  chatId: number = 0;
  messages: Message[] = [];
  newMessage = '';
  isLoading = false;

  isBotTyping = false;
  isStreaming = false;
  streamingContent = '';

  private shouldScrollToBottom = false;

  constructor(
    private route: ActivatedRoute,
    private apiService: ApiService,
    private signalrService: SignalrService,
    private ngZone: NgZone,
    @Inject(PLATFORM_ID) private platformId: Object
  ) { }

  ngOnInit(): void {
    if (!isPlatformBrowser(this.platformId)) return;

    this.chatId = Number(this.route.snapshot.paramMap.get('id'));
    if (!this.chatId) return;

    this.loadMessages();
    this.signalrService.startConnection();
    this.signalrService.joinChat(this.chatId);
    this.registerSignalRListeners();
  }

  ngAfterViewChecked(): void {
    if (this.shouldScrollToBottom) {
      this.scrollToBottom();
      this.shouldScrollToBottom = false;
    }
  }

  ngOnDestroy(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    this.signalrService.leaveChat(this.chatId);
    this.signalrService.removeMessageListener();
    this.signalrService.removeUserMessageListener();
    this.signalrService.removeBotTypingListener();
    this.signalrService.removeMessageChunkListener();
    this.signalrService.removeMessageStreamCompleteListener();
    this.signalrService.removeMessageStreamErrorListener();
    this.signalrService.stopConnection();
  }

  private registerSignalRListeners(): void {
    // User message confirmed from server (could arrive from another client too)
    this.signalrService.addUserMessageListener((msg: any) => {
      this.ngZone.run(() => {
        const alreadyExists = this.messages.some(m => m.id === msg.id);
        if (!alreadyExists) {
          // Replace optimistic (-1) if present, otherwise push
          const optimisticIdx = this.messages.findIndex(m => m.id === -1 && m.role === 'user');
          if (optimisticIdx !== -1) this.messages[optimisticIdx] = msg as Message;
          else this.messages.push(msg as Message);
        }
        this.shouldScrollToBottom = true;
      });
    });

    // Bot is thinking (embedding + vector search)
    this.signalrService.addBotTypingListener(() => {
      this.ngZone.run(() => {
        this.isBotTyping = true;
        this.isStreaming = false;
        this.streamingContent = '';
        this.shouldScrollToBottom = true;
      });
    });

    // Streaming token arrived
    this.signalrService.addMessageChunkListener((_chatId: number, token: string) => {
      this.ngZone.run(() => {
        this.isBotTyping = false;
        this.isStreaming = true;
        this.streamingContent += token;
        this.shouldScrollToBottom = true;
      });
    });

    // Stream complete — add formatted message
    this.signalrService.addMessageStreamCompleteListener((msg: any) => {
      this.ngZone.run(() => {
        this.isBotTyping = false;
        this.isStreaming = false;
        this.streamingContent = '';
        this.messages.push(msg as Message);
        this.shouldScrollToBottom = true;
      });
    });

    // Stream error
    this.signalrService.addMessageStreamErrorListener((_chatId: number, error: string) => {
      this.ngZone.run(() => {
        this.isBotTyping = false;
        this.isStreaming = false;
        this.streamingContent = '';
        this.messages.push({
          id: Date.now(),
          chatId: this.chatId,
          role: 'assistant',
          content: `Sorry, an error occurred: ${error}`,
          createdAt: new Date().toISOString()
        } as Message);
        this.shouldScrollToBottom = true;
      });
    });
  }

  loadMessages(): void {
    this.apiService.getMessages(this.chatId).subscribe({
      next: (messages) => {
        this.messages = messages;
        this.shouldScrollToBottom = true;
      },
      error: (err) => console.error('Error loading messages:', err)
    });
  }

  sendMessage(): void {
    const content = this.newMessage.trim();
    if (!content || this.isLoading || this.isBotTyping || this.isStreaming) return;

    this.newMessage = '';
    this.isLoading = true;

    // Show user message immediately (optimistic)
    const optimistic: Message = {
      id: -1,
      chatId: this.chatId,
      role: 'user',
      content,
      createdAt: new Date().toISOString()
    };
    this.messages.push(optimistic);
    this.shouldScrollToBottom = true;

    this.apiService.sendMessage(this.chatId, content).subscribe({
      next: (response: any) => {
        this.isLoading = false;
        // Update optimistic message with real server ID
        if (response?.userMessageId) {
          const idx = this.messages.findIndex(m => m.id === -1 && m.role === 'user');
          if (idx !== -1) this.messages[idx] = { ...this.messages[idx], id: response.userMessageId };
        }
      },
      error: (err) => {
        console.error('Error sending message:', err);
        this.isLoading = false;
        this.isBotTyping = false;
        this.isStreaming = false;
        this.messages = this.messages.filter(m => m.id !== -1);
      }
    });
  }

  get isBusy(): boolean {
    return this.isLoading || this.isBotTyping || this.isStreaming;
  }

  private scrollToBottom(): void {
    try {
      const el = this.messagesContainer?.nativeElement;
      if (el) el.scrollTop = el.scrollHeight;
    } catch { /* ignore */ }
  }

  onKeyPress(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.sendMessage();
    }
  }

  parseSources(sources: string): any[] {
    try { return JSON.parse(sources); }
    catch { return []; }
  }
}