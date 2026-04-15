import { Component, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { DocumentMetadata } from '../../core/models/document.model';
import { DocumentService } from '../../core/services/document.service';
import { FileUploadComponent } from './file-upload/file-upload.component';
import { DocumentListComponent } from './document-list/document-list.component';

@Component({
  selector: 'app-document-manager',
  standalone: true,
  imports: [FileUploadComponent, DocumentListComponent, MatButtonModule, MatIconModule],
  template: `
    <div class="document-manager">
      <div class="upload-section">
        <h2>Upload Documents</h2>
        <app-file-upload (documentUploaded)="onDocumentUploaded($event)" />
      </div>

      <div class="list-section">
        <div class="list-header">
          <h2>Uploaded Documents</h2>
          @if (selectedIds().size > 0) {
            <button
              mat-raised-button
              color="warn"
              (click)="onDelete()"
              [disabled]="isDeleting()">
              <mat-icon>delete</mat-icon>
              Delete ({{ selectedIds().size }})
            </button>
          }
        </div>
        <app-document-list
          [documents]="documents()"
          [selectedIds]="selectedIds()"
          (selectionChange)="selectedIds.set($event)" />
      </div>
    </div>
  `,
  styles: [`
    .document-manager {
      padding: 24px;
      max-width: 900px;
      margin: 0 auto;
    }
    .upload-section {
      margin-bottom: 32px;
    }
    .list-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 16px;
    }
    h2 {
      font-weight: 500;
      margin: 0;
    }
  `]
})
export class DocumentManagerComponent implements OnInit {
  documents = signal<DocumentMetadata[]>([]);
  selectedIds = signal<Set<string>>(new Set());
  isDeleting = signal(false);

  constructor(private readonly documentService: DocumentService) {}

  ngOnInit(): void {
    this.loadDocuments();
  }

  loadDocuments(): void {
    this.documentService.list().subscribe({
      next: docs => this.documents.set(docs),
      error: err => console.error('Failed to load documents:', err)
    });
  }

  onDocumentUploaded(doc: DocumentMetadata): void {
    this.documents.update(docs => [doc, ...docs]);
  }

  onDelete(): void {
    const ids = [...this.selectedIds()];
    const count = ids.length;
    const noun = count === 1 ? 'document' : 'documents';
    if (!confirm(`Delete ${count} ${noun}? This will permanently remove them from all data stores and cannot be undone.`)) {
      return;
    }

    this.isDeleting.set(true);
    this.documentService.delete(ids).subscribe({
      next: () => {
        this.selectedIds.set(new Set());
        this.isDeleting.set(false);
        this.loadDocuments();
      },
      error: err => {
        console.error('Failed to delete documents:', err);
        this.isDeleting.set(false);
      }
    });
  }
}
