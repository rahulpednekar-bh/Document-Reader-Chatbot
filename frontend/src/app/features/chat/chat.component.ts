import { Component, OnInit, signal } from '@angular/core';
import { ChatSession, ChatMessage } from '../../core/models/chat.model';
import { ChatService } from '../../core/services/chat.service';
import { ChatHistorySidebarComponent } from './chat-history-sidebar/chat-history-sidebar.component';
import { ChatWindowComponent } from './chat-window/chat-window.component';
import { ChatInputComponent } from './chat-input/chat-input.component';

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [ChatHistorySidebarComponent, ChatWindowComponent, ChatInputComponent],
  template: `
    <div class="chat-layout">
      <app-chat-history-sidebar
        [sessions]="sessions()"
        [activeSessionId]="activeSession()?.id ?? null"
        (sessionSelected)="onSessionSelected($event)"
        (newSession)="onNewSession()" />

      <div class="chat-main">
        @if (activeSession()) {
          <app-chat-window [messages]="messages()" [isLoading]="isLoading()" />
          <app-chat-input
            [disabled]="isLoading()"
            (messageSent)="onMessageSent($event)" />
        } @else {
          <div class="empty-state">
            <p>Select a session from the sidebar or start a new chat.</p>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .chat-layout {
      display: grid;
      grid-template-columns: 260px 1fr;
      height: 100%;
      overflow: hidden;
    }
    .chat-main {
      display: flex;
      flex-direction: column;
      overflow: hidden;
    }
    .empty-state {
      display: flex;
      align-items: center;
      justify-content: center;
      height: 100%;
      color: #9e9e9e;
    }
  `]
})
export class ChatComponent implements OnInit {
  sessions = signal<ChatSession[]>([]);
  activeSession = signal<ChatSession | null>(null);
  messages = signal<ChatMessage[]>([]);
  isLoading = signal(false);

  constructor(private readonly chatService: ChatService) {}

  ngOnInit(): void {
    this.chatService.getSessions().subscribe(s => this.sessions.set(s));
  }

  onNewSession(): void {
    this.chatService.createSession().subscribe(session => {
      this.sessions.update(s => [session, ...s]);
      this.activeSession.set(session);
      this.messages.set([]);
    });
  }

  onSessionSelected(session: ChatSession): void {
    this.activeSession.set(session);
    this.chatService.getMessages(session.id).subscribe(msgs => this.messages.set(msgs));
  }

  onMessageSent(content: string): void {
    const userMessage: ChatMessage = {
      role: 'user',
      content,
      createdAt: new Date().toISOString()
    };
    this.messages.update(msgs => [...msgs, userMessage]);
    this.isLoading.set(true);

    this.chatService.sendMessage(this.activeSession()!.id, content).subscribe({
      next: response => {
        this.messages.update(msgs => [...msgs, response.assistantMessage]);
        this.isLoading.set(false);
      },
      error: () => {
        this.messages.update(msgs => [...msgs, {
          role: 'assistant',
          content: 'Sorry, an error occurred. Please try again.',
          createdAt: new Date().toISOString()
        }]);
        this.isLoading.set(false);
      }
    });
  }
}
