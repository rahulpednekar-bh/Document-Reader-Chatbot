import { Component, OnInit, signal } from '@angular/core';
import { DocumentMetadata } from '../../core/models/document.model';
import { DocumentService } from '../../core/services/document.service';
import { FileUploadComponent } from './file-upload/file-upload.component';
import { DocumentListComponent } from './document-list/document-list.component';

@Component({
  selector: 'app-document-manager',
  standalone: true,
  imports: [FileUploadComponent, DocumentListComponent],
  template: `
    <div class="document-manager">
      <div class="upload-section">
        <h2>Upload Documents</h2>
        <app-file-upload (documentUploaded)="onDocumentUploaded($event)" />
      </div>
      <div class="list-section">
        <h2>Uploaded Documents</h2>
        <app-document-list [documents]="documents()" />
      </div>
    </div>
  `,
  styles: [`
    .document-manager {
      padding: 24px;
      max-width: 900px;
      margin: 0 auto;
    }
    .upload-section, .list-section {
      margin-bottom: 32px;
    }
    h2 {
      margin-bottom: 16px;
      font-weight: 500;
    }
  `]
})
export class DocumentManagerComponent implements OnInit {
  documents = signal<DocumentMetadata[]>([]);

  constructor(private readonly documentService: DocumentService) {}

  ngOnInit(): void {
    this.loadDocuments();
  }

  loadDocuments(): void {
    this.documentService.list().subscribe(docs => this.documents.set(docs));
  }

  onDocumentUploaded(doc: DocumentMetadata): void {
    this.documents.update(docs => [doc, ...docs]);
  }
}
