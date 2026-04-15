import { Component, ElementRef, Input, OnChanges, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { marked } from 'marked';
import DOMPurify from 'dompurify';
import { ChatMessage, DocumentCitation } from '../../../core/models/chat.model';

@Component({
  selector: 'app-chat-window',
  standalone: true,
  imports: [CommonModule, MatProgressSpinnerModule, MatChipsModule, MatIconModule],
  template: `
    <div #scrollContainer class="chat-window">
      @for (message of messages; track $index) {
        <div class="message-row" [class.user]="message.role === 'user'">
          <div class="message-group">
            <div class="bubble" [class.user-bubble]="message.role === 'user'">
              @if (message.role === 'assistant') {
                <div [innerHTML]="renderMarkdown(message.content)"></div>
              } @else {
                {{ message.content }}
              }
            </div>
            @if (message.role === 'assistant' && message.citations && message.citations.length > 0) {
              <div class="citations">
                <mat-icon class="citations-icon">menu_book</mat-icon>
                <span class="citations-label">Sources:</span>
                <mat-chip-set>
                  @for (citation of message.citations; track citation.fileName) {
                    <mat-chip class="citation-chip">
                      <mat-icon matChipAvatar>description</mat-icon>
                      {{ citation.fileName }}
                      @if (citation.pageNumbers && citation.pageNumbers.length > 0) {
                        <span class="page-numbers">
                          &nbsp;· p.&nbsp;{{ citation.pageNumbers.join(', ') }}
                        </span>
                      }
                    </mat-chip>
                  }
                </mat-chip-set>
              </div>
            }
          </div>
        </div>
      }
      @if (isLoading) {
        <div class="message-row">
          <div class="bubble loading-bubble">
            <mat-spinner diameter="20" />
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .chat-window {
      flex: 1;
      overflow-y: auto;
      padding: 16px;
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .message-row {
      display: flex;
      justify-content: flex-start;
    }
    .message-row.user {
      justify-content: flex-end;
    }
    .message-group {
      display: flex;
      flex-direction: column;
      max-width: 70%;
      gap: 6px;
    }
    .bubble {
      padding: 10px 14px;
      border-radius: 12px;
      background: #f0f0f0;
      font-size: 14px;
      line-height: 1.6;
      word-wrap: break-word;
    }
    .user-bubble {
      background: #3f51b5;
      color: white;
    }
    .loading-bubble {
      display: flex;
      align-items: center;
      padding: 12px;
    }
    .citations {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 4px;
      padding: 4px 2px;
    }
    .citations-icon {
      font-size: 16px;
      height: 16px;
      width: 16px;
      color: #757575;
    }
    .citations-label {
      font-size: 12px;
      color: #757575;
      margin-right: 4px;
    }
    .citation-chip {
      font-size: 12px;
    }
    .page-numbers {
      color: #757575;
    }
    :host ::ng-deep .bubble pre {
      background: #282c34;
      color: #abb2bf;
      padding: 12px;
      border-radius: 6px;
      overflow-x: auto;
      font-size: 13px;
    }
    :host ::ng-deep .bubble code {
      font-family: 'Courier New', monospace;
    }
  `]
})
export class ChatWindowComponent implements OnChanges {
  @Input() messages: ChatMessage[] = [];
  @Input() isLoading = false;
  @ViewChild('scrollContainer') private scrollContainer!: ElementRef<HTMLDivElement>;

  private readonly sanitizer = inject(DomSanitizer);

  ngOnChanges(): void {
    setTimeout(() => this.scrollToBottom(), 50);
  }

  renderMarkdown(content: string): SafeHtml {
    const html = marked.parse(content, { async: false }) as string;
    const clean = DOMPurify.sanitize(html);
    return this.sanitizer.bypassSecurityTrustHtml(clean);
  }

  private scrollToBottom(): void {
    if (this.scrollContainer?.nativeElement) {
      this.scrollContainer.nativeElement.scrollTop =
        this.scrollContainer.nativeElement.scrollHeight;
    }
  }
}
