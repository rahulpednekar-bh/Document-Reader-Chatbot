import { Component, Input } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { DocumentMetadata } from '../../../core/models/document.model';

@Component({
  selector: 'app-document-list',
  standalone: true,
  imports: [CommonModule, MatListModule, MatIconModule, MatChipsModule, DatePipe],
  template: `
    @if (documents.length === 0) {
      <p class="empty-state">No documents uploaded yet.</p>
    } @else {
      <mat-list>
        @for (doc of documents; track doc.id) {
          <mat-list-item>
            <mat-icon matListItemIcon>
              {{ doc.fileName.endsWith('.pdf') ? 'picture_as_pdf' : 'article' }}
            </mat-icon>
            <span matListItemTitle>{{ doc.fileName }}</span>
            <span matListItemLine>
              {{ doc.sizeBytes | number }} bytes &bull;
              {{ doc.uploadedAt | date:'medium' }}
            </span>
            <span matListItemMeta>
              <mat-chip [color]="statusColor(doc.status)" highlighted>
                {{ doc.status }}
              </mat-chip>
            </span>
          </mat-list-item>
        }
      </mat-list>
    }
  `,
  styles: [`
    .empty-state {
      color: #9e9e9e;
      text-align: center;
      padding: 24px;
    }
    mat-list-item {
      border-bottom: 1px solid #f0f0f0;
    }
  `]
})
export class DocumentListComponent {
  @Input() documents: DocumentMetadata[] = [];

  statusColor(status: string): 'primary' | 'accent' | 'warn' {
    switch (status) {
      case 'indexed': return 'primary';
      case 'processing': return 'accent';
      case 'failed': return 'warn';
      default: return 'accent';
    }
  }
}
