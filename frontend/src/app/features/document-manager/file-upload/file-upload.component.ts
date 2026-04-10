import { Component, EventEmitter, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { DocumentMetadata, UploadProgress } from '../../../core/models/document.model';
import { DocumentService } from '../../../core/services/document.service';

@Component({
  selector: 'app-file-upload',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatProgressBarModule, MatIconModule, MatChipsModule],
  template: `
    <div
      class="drop-zone"
      [class.drag-over]="isDragOver()"
      (dragover)="onDragOver($event)"
      (dragleave)="onDragLeave()"
      (drop)="onDrop($event)">

      <mat-icon class="upload-icon">cloud_upload</mat-icon>
      <p>Drag & drop a file here, or</p>
      <button mat-raised-button color="primary" (click)="fileInput.click()">
        Browse File
      </button>
      <p class="hint">Supported: .pdf, .docx — max 25 MB</p>

      <input
        #fileInput
        type="file"
        accept=".pdf,.docx"
        hidden
        (change)="onFileSelected($event)" />
    </div>

    @if (validationError()) {
      <mat-chip-set>
        <mat-chip color="warn" highlighted>
          <mat-icon matChipAvatar>error</mat-icon>
          {{ validationError() }}
        </mat-chip>
      </mat-chip-set>
    }

    @if (uploadProgress(); as progress) {
      <div class="progress-container">
        <span class="file-name">{{ selectedFileName() }}</span>
        <mat-progress-bar
          mode="determinate"
          [value]="progress.percent"
          [color]="progress.status === 'error' ? 'warn' : 'primary'">
        </mat-progress-bar>
        <span class="progress-label">
          @if (progress.status === 'complete') {
            <mat-icon class="success-icon">check_circle</mat-icon> Uploaded successfully
          } @else if (progress.status === 'error') {
            <mat-icon color="warn">error</mat-icon> {{ progress.error }}
          } @else {
            {{ progress.percent }}%
          }
        </span>
      </div>
    }
  `,
  styles: [`
    .drop-zone {
      border: 2px dashed #ccc;
      border-radius: 8px;
      padding: 40px;
      text-align: center;
      cursor: pointer;
      transition: border-color 0.2s, background-color 0.2s;
    }
    .drop-zone.drag-over {
      border-color: #3f51b5;
      background-color: #f0f4ff;
    }
    .upload-icon {
      font-size: 48px;
      height: 48px;
      width: 48px;
      color: #9e9e9e;
    }
    .hint {
      font-size: 12px;
      color: #9e9e9e;
      margin-top: 8px;
    }
    .progress-container {
      margin-top: 16px;
    }
    .file-name {
      font-size: 14px;
      display: block;
      margin-bottom: 8px;
      font-weight: 500;
    }
    .progress-label {
      font-size: 12px;
      display: flex;
      align-items: center;
      gap: 4px;
      margin-top: 4px;
    }
    .success-icon {
      color: #4caf50;
      font-size: 16px;
      height: 16px;
      width: 16px;
    }
  `]
})
export class FileUploadComponent {
  @Output() documentUploaded = new EventEmitter<DocumentMetadata>();

  isDragOver = signal(false);
  validationError = signal<string | null>(null);
  uploadProgress = signal<UploadProgress | null>(null);
  selectedFileName = signal<string>('');

  constructor(private readonly documentService: DocumentService) {}

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) this.processFile(file);
    input.value = '';
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(true);
  }

  onDragLeave(): void {
    this.isDragOver.set(false);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(false);
    const file = event.dataTransfer?.files[0];
    if (file) this.processFile(file);
  }

  private processFile(file: File): void {
    this.validationError.set(null);
    this.uploadProgress.set(null);

    const error = this.documentService.validate(file);
    if (error) {
      this.validationError.set(error);
      return;
    }

    this.selectedFileName.set(file.name);
    this.uploadProgress.set({ percent: 0, status: 'pending' });

    this.documentService.upload(file).subscribe({
      next: progress => this.uploadProgress.set(progress),
      error: () => this.uploadProgress.set({
        percent: 0,
        status: 'error',
        error: 'Upload failed. Please try again.'
      }),
      complete: () => {
        // Reload the document list entry via parent
        this.documentService.list().subscribe(docs => {
          const uploaded = docs.find(d => d.fileName === file.name);
          if (uploaded) this.documentUploaded.emit(uploaded);
        });
      }
    });
  }
}
