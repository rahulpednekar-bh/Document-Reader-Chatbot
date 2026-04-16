import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DocumentMetadata } from '../../../core/models/document.model';

@Component({
  selector: 'app-document-list',
  standalone: true,
  imports: [CommonModule, MatListModule, MatIconModule, MatChipsModule, MatCheckboxModule, MatTooltipModule, DatePipe],
  template: `
    @if (documents.length === 0) {
      <p class="empty-state">No documents uploaded yet.</p>
    } @else {
      <div class="list-header">
        <mat-checkbox
          [checked]="allSelected"
          [indeterminate]="someSelected"
          (change)="toggleAll()"
          aria-label="Select all documents">
          Select all ({{ documents.length }})
        </mat-checkbox>
      </div>

      <mat-list>
        @for (doc of documents; track doc.id) {
          <mat-list-item (click)="toggleSelection(doc.id)" class="document-row">
            <span matListItemIcon>
              <mat-checkbox
                [checked]="selectedIds.has(doc.id)"
                (change)="toggleSelection(doc.id)"
                (click)="$event.stopPropagation()"
                [aria-label]="'Select ' + doc.fileName" />
            </span>
            <mat-icon matListItemIcon>
              {{ doc.fileName.endsWith('.pdf') ? 'picture_as_pdf' : 'article' }}
            </mat-icon>
            <span matListItemTitle>{{ doc.fileName }}</span>
            <span matListItemLine>
              {{ doc.sizeBytes | number }} bytes &bull;
              {{ doc.uploadedAt | date:'medium' }}
            </span>
            <span matListItemMeta class="chip-group">
              <mat-chip [color]="statusColor(doc.status)" highlighted>
                {{ statusLabel(doc.status) }}
              </mat-chip>
              @if (doc.ocrApplied) {
                <mat-chip
                  color="primary"
                  highlighted
                  class="ocr-chip"
                  [matTooltip]="'This scanned PDF was processed with OCR to extract text before indexing.'">
                  <mat-icon matChipAvatar>document_scanner</mat-icon>
                  OCR Applied
                </mat-chip>
              }
              @if (doc.status === 'failed' && doc.processingNote) {
                <mat-icon
                  color="warn"
                  class="info-icon"
                  [matTooltip]="doc.processingNote">
                  info
                </mat-icon>
              }
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
    .list-header {
      padding: 8px 16px;
      border-bottom: 1px solid #e0e0e0;
      background: #fafafa;
    }
    .document-row {
      border-bottom: 1px solid #f0f0f0;
      cursor: pointer;
    }
    .document-row:hover {
      background: #f5f5f5;
    }
    .chip-group {
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .ocr-chip {
      background-color: #1565c0 !important;
      color: white !important;
      font-size: 11px;
    }
    .info-icon {
      font-size: 18px;
      height: 18px;
      width: 18px;
      cursor: help;
    }
  `]
})
export class DocumentListComponent {
  @Input() documents: DocumentMetadata[] = [];
  @Input() selectedIds: Set<string> = new Set();
  @Output() selectionChange = new EventEmitter<Set<string>>();

  get allSelected(): boolean {
    return this.documents.length > 0 && this.selectedIds.size === this.documents.length;
  }

  get someSelected(): boolean {
    return this.selectedIds.size > 0 && this.selectedIds.size < this.documents.length;
  }

  toggleSelection(id: string): void {
    const next = new Set(this.selectedIds);
    if (next.has(id)) {
      next.delete(id);
    } else {
      next.add(id);
    }
    this.selectionChange.emit(next);
  }

  toggleAll(): void {
    if (this.allSelected) {
      this.selectionChange.emit(new Set());
    } else {
      this.selectionChange.emit(new Set(this.documents.map(d => d.id)));
    }
  }

  statusColor(status: string): 'primary' | 'accent' | 'warn' {
    switch (status) {
      case 'indexed': return 'primary';
      case 'processing': return 'accent';
      case 'failed': return 'warn';
      default: return 'accent';
    }
  }

  statusLabel(status: string): string {
    switch (status) {
      case 'indexed': return 'Ready';
      case 'processing': return 'Processing';
      case 'failed': return 'Failed';
      default: return status;
    }
  }
}
