import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';

@Component({
  selector: 'app-chat-input',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatIconModule, MatInputModule, MatFormFieldModule],
  template: `
    <div class="input-bar">
      <mat-form-field class="message-field" appearance="outline" subscriptSizing="dynamic">
        <textarea
          matInput
          [(ngModel)]="message"
          [disabled]="disabled"
          placeholder="Ask a question about your documents..."
          rows="1"
          cdkTextareaAutosize
          (keydown.enter)="onEnter($event)">
        </textarea>
      </mat-form-field>
      <button
        mat-icon-button
        color="primary"
        [disabled]="disabled || !message.trim()"
        (click)="send()">
        <mat-icon>send</mat-icon>
      </button>
    </div>
  `,
  styles: [`
    .input-bar {
      display: flex;
      align-items: flex-end;
      gap: 8px;
      padding: 12px 16px;
      border-top: 1px solid #e0e0e0;
      background: white;
    }
    .message-field {
      flex: 1;
    }
  `]
})
export class ChatInputComponent {
  @Input() disabled = false;
  @Output() messageSent = new EventEmitter<string>();

  message = '';

  onEnter(event: Event): void {
    const keyEvent = event as KeyboardEvent;
    if (!keyEvent.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  send(): void {
    const text = this.message.trim();
    if (!text || this.disabled) return;
    this.message = '';
    this.messageSent.emit(text);
  }
}
