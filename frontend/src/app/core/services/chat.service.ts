import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ChatMessage, ChatSession, SendMessageResponse } from '../models/chat.model';

@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  createSession(title?: string): Observable<ChatSession> {
    return this.http.post<ChatSession>(`${this.baseUrl}/sessions`, { title: title ?? 'New Chat' });
  }

  getSessions(): Observable<ChatSession[]> {
    return this.http.get<ChatSession[]>(`${this.baseUrl}/sessions`);
  }

  getMessages(sessionId: string): Observable<ChatMessage[]> {
    return this.http.get<ChatMessage[]>(`${this.baseUrl}/sessions/${sessionId}/messages`);
  }

  sendMessage(sessionId: string, content: string): Observable<SendMessageResponse> {
    return this.http.post<SendMessageResponse>(
      `${this.baseUrl}/sessions/${sessionId}/messages`,
      { content }
    );
  }

  deleteSession(sessionId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/sessions/${sessionId}`);
  }
}
