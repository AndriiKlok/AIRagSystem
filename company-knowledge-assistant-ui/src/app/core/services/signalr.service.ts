import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { BehaviorSubject } from 'rxjs';
import { SIGNALR_HUB_URL } from '../config/api.config';

@Injectable({
  providedIn: 'root'
})
export class SignalrService {
  private hubConnection: HubConnection | null = null;
  private startPromise: Promise<void> | null = null;
  private connectionStatus = new BehaviorSubject<boolean>(false);

  connectionStatus$ = this.connectionStatus.asObservable();

  startConnection(): Promise<void> {
    if (!this.hubConnection) {
      this.hubConnection = new HubConnectionBuilder()
        .withUrl(SIGNALR_HUB_URL)
        .withAutomaticReconnect()
        .build();

      this.hubConnection.onreconnected(() => {
        console.log('SignalR reconnected');
        this.connectionStatus.next(true);
      });

      this.hubConnection.onclose(() => {
        console.log('SignalR connection closed');
        this.connectionStatus.next(false);
      });
    }

    if (this.hubConnection.state === HubConnectionState.Connected) {
      this.connectionStatus.next(true);
      return Promise.resolve();
    }

    if (this.startPromise) {
      return this.startPromise;
    }

    this.startPromise = this.hubConnection.start()
      .then(() => {
        console.log('SignalR connection started');
        this.connectionStatus.next(true);
      })
      .catch(err => {
        console.error('Error starting SignalR connection:', err);
        this.connectionStatus.next(false);
        throw err;
      })
      .finally(() => {
        this.startPromise = null;
      });

    return this.startPromise;
  }

  private invokeWhenConnected(methodName: string, ...args: any[]): void {
    this.startConnection()
      .then(() => {
        if (this.hubConnection && this.hubConnection.state === HubConnectionState.Connected) {
          return this.hubConnection.invoke(methodName, ...args);
        }
        return Promise.reject(new Error(`SignalR connection is not connected for method ${methodName}.`));
      })
      .catch(err => {
        console.error(`Error invoking ${methodName}:`, err);
      });
  }

  joinChat(chatId: number): void {
    this.invokeWhenConnected('JoinChat', chatId);
  }

  leaveChat(chatId: number): void {
    if (this.hubConnection && this.hubConnection.state === HubConnectionState.Connected) {
      this.hubConnection.invoke('LeaveChat', chatId).catch(err => console.error('Error invoking LeaveChat:', err));
    }
  }

  joinArea(areaId: number): void {
    this.invokeWhenConnected('JoinArea', areaId);
  }

  leaveArea(areaId: number): void {
    if (this.hubConnection && this.hubConnection.state === HubConnectionState.Connected) {
      this.hubConnection.invoke('LeaveArea', areaId).catch(err => console.error('Error invoking LeaveArea:', err));
    }
  }

  addDocumentProgressListener(callback: (progress: any) => void): void {
    if (this.hubConnection) {
      this.hubConnection.on('DocumentProgress', callback);
    }
  }

  removeDocumentProgressListener(): void {
    if (this.hubConnection) {
      this.hubConnection.off('DocumentProgress');
    }
  }

  addMessageListener(callback: (message: any) => void): void {
    if (this.hubConnection) {
      this.hubConnection.on('ReceiveMessage', callback);
    }
  }

  removeMessageListener(): void {
    if (this.hubConnection) {
      this.hubConnection.off('ReceiveMessage');
    }
  }

  addUserMessageListener(callback: (message: any) => void): void {
    this.hubConnection?.on('ReceiveUserMessage', callback);
  }
  removeUserMessageListener(): void {
    this.hubConnection?.off('ReceiveUserMessage');
  }

  addBotTypingListener(callback: (chatId: number) => void): void {
    this.hubConnection?.on('BotTyping', callback);
  }
  removeBotTypingListener(): void {
    this.hubConnection?.off('BotTyping');
  }

  addMessageChunkListener(callback: (chatId: number, token: string) => void): void {
    this.hubConnection?.on('ReceiveMessageChunk', callback);
  }
  removeMessageChunkListener(): void {
    this.hubConnection?.off('ReceiveMessageChunk');
  }

  addMessageStreamCompleteListener(callback: (message: any) => void): void {
    this.hubConnection?.on('MessageStreamComplete', callback);
  }
  removeMessageStreamCompleteListener(): void {
    this.hubConnection?.off('MessageStreamComplete');
  }

  addMessageStreamErrorListener(callback: (chatId: number, error: string) => void): void {
    this.hubConnection?.on('MessageStreamError', callback);
  }
  removeMessageStreamErrorListener(): void {
    this.hubConnection?.off('MessageStreamError');
  }

  stopConnection(): void {
    if (this.hubConnection) {
      this.hubConnection.stop().catch(err => console.error('Error stopping SignalR connection:', err));
      this.startPromise = null;
      this.connectionStatus.next(false);
    }
  }

  sendMessage(chatId: number, message: any): void {
    this.invokeWhenConnected('SendMessage', chatId, message);
  }
}