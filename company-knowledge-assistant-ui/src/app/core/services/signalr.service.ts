import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { BehaviorSubject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class SignalrService {
  private hubConnection: HubConnection | null = null;
  private connectionStatus = new BehaviorSubject<boolean>(false);

  connectionStatus$ = this.connectionStatus.asObservable();

  startConnection(): void {
    this.hubConnection = new HubConnectionBuilder()
      .withUrl('http://localhost:5000/chatHub')
      .withAutomaticReconnect()
      .build();

    this.hubConnection.start()
      .then(() => {
        console.log('SignalR connection started');
        this.connectionStatus.next(true);
      })
      .catch(err => {
        console.error('Error starting SignalR connection:', err);
        this.connectionStatus.next(false);
      });

    this.hubConnection.onreconnected(() => {
      console.log('SignalR reconnected');
      this.connectionStatus.next(true);
    });

    this.hubConnection.onclose(() => {
      console.log('SignalR connection closed');
      this.connectionStatus.next(false);
    });
  }

  joinChat(chatId: number): void {
    if (this.hubConnection) {
      this.hubConnection.invoke('JoinChat', chatId);
    }
  }

  leaveChat(chatId: number): void {
    if (this.hubConnection) {
      this.hubConnection.invoke('LeaveChat', chatId);
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

  stopConnection(): void {
    if (this.hubConnection) {
      this.hubConnection.stop();
      this.connectionStatus.next(false);
    }
  }

  sendMessage(chatId: number, message: any): void {
    if (this.hubConnection) {
      this.hubConnection.invoke('SendMessage', chatId, message);
    }
  }
}