import { Injectable } from '@angular/core';
import { HttpClient, HttpRequest, HttpEvent } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';

export interface Area {
  id: number;
  name: string;
  description?: string;
  createdAt: string;
  updatedAt: string;
  documentCount: number;
  chatCount: number;
}

export interface Document {
  id: number;
  areaId: number;
  fileName: string;
  filePath: string;
  fileSize: number;
  uploadedAt: string;
  processingStatus: string;
  chunkCount: number;
  errorMessage?: string;
}

export interface Chat {
  id: number;
  areaId: number;
  name: string;
  createdAt: string;
  lastMessageAt?: string;
  messageCount: number;
}

export interface Message {
  id: number;
  chatId: number;
  role: string;
  content: string;
  contentHtml?: string;
  sources?: string;
  createdAt: string;
}

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private baseUrl = API_BASE_URL;

  constructor(private http: HttpClient) { }

  // Areas
  getAreas(): Observable<Area[]> {
    return this.http.get<Area[]>(`${this.baseUrl}/areas`);
  }

  getArea(id: number): Observable<Area> {
    return this.http.get<Area>(`${this.baseUrl}/areas/${id}`);
  }

  createArea(area: { name: string; description?: string }): Observable<Area> {
    return this.http.post<Area>(`${this.baseUrl}/areas`, area);
  }

  updateArea(id: number, area: { name: string; description?: string }): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/areas/${id}`, area);
  }

  deleteArea(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/areas/${id}`);
  }

  // Documents
  getDocuments(areaId: number): Observable<Document[]> {
    return this.http.get<Document[]>(`${this.baseUrl}/documents?areaId=${areaId}`);
  }

  getDocument(id: number): Observable<Document> {
    return this.http.get<Document>(`${this.baseUrl}/documents/${id}`);
  }

  uploadDocument(areaId: number, file: File): Observable<any> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post(`${this.baseUrl}/documents/upload?areaId=${areaId}`, formData);
  }

  uploadDocumentWithProgress(areaId: number, file: File): Observable<HttpEvent<any>> {
    const formData = new FormData();
    formData.append('file', file);
    const req = new HttpRequest('POST', `${this.baseUrl}/documents/upload?areaId=${areaId}`, formData, {
      reportProgress: true
    });
    return this.http.request(req);
  }

  analyzeDocument(id: number): Observable<any> {
    return this.http.post(`${this.baseUrl}/documents/${id}/analyze`, {});
  }

  deleteDocument(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/documents/${id}`);
  }

  // Chats
  getChats(areaId: number): Observable<Chat[]> {
    return this.http.get<Chat[]>(`${this.baseUrl}/chats?areaId=${areaId}`);
  }

  getChat(id: number): Observable<Chat> {
    return this.http.get<Chat>(`${this.baseUrl}/chats/${id}`);
  }

  createChat(chat: { areaId: number; name?: string }): Observable<Chat> {
    return this.http.post<Chat>(`${this.baseUrl}/chats`, chat);
  }

  updateChat(id: number, chat: { name: string }): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/chats/${id}`, chat);
  }

  deleteChat(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/chats/${id}`);
  }

  // Messages
  getMessages(chatId: number): Observable<Message[]> {
    return this.http.get<Message[]>(`${this.baseUrl}/messages?chatId=${chatId}`);
  }

  sendMessage(chatId: number, content: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/messages`, { chatId, content });
  }
}