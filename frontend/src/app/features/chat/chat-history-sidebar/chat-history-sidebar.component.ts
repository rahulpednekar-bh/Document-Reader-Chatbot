import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { MatListModule } from '@angular/material/list';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { ChatSession } from '../../../core/models/chat.model';

@Component({
  selector: 'app-chat-history-sidebar',
  standalone: true,
  imports: [CommonModule, MatListModule, MatButtonModule, MatIconModule, MatDividerModule, DatePipe],
  template: `
    <div class="sidebar">
      <div class="sidebar-header">
        <button mat-raised-button color="primary" (click)="newSession.emit()">
          <mat-icon>add</mat-icon> New Chat
        </button>
      </div>
      <mat-divider />
      <mat-nav-list>
        @for (session of sessions; track session.id) {
          <mat-list-item
            [activated]="session.id === activeSessionId"
            (click)="sessionSelected.emit(session)">
            <mat-icon matListItemIcon>chat_bubble_outline</mat-icon>
            <span matListItemTitle>{{ session.title }}</span>
            <span matListItemLine>{{ session.createdAt | date:'shortDate' }}</span>
          </mat-list-item>
        }
        @if (sessions.length === 0) {
          <div class="no-sessions">No previous chats</div>
        }
      </mat-nav-list>
    </div>
  `,
  styles: [`
    .sidebar {
      height: 100%;
      border-right: 1px solid #e0e0e0;
      display: flex;
      flex-direction: column;
      overflow: hidden;
    }
    .sidebar-header {
      padding: 16px;
    }
    .sidebar-header button {
      width: 100%;
    }
    mat-nav-list {
      overflow-y: auto;
      flex: 1;
    }
    .no-sessions {
      padding: 16px;
      color: #9e9e9e;
      font-size: 14px;
      text-align: center;
    }
    mat-list-item {
      cursor: pointer;
    }
  `]
})
export class ChatHistorySidebarComponent {
  @Input() sessions: ChatSession[] = [];
  @Input() activeSessionId: string | null = null;
  @Output() sessionSelected = new EventEmitter<ChatSession>();
  @Output() newSession = new EventEmitter<void>();
}
