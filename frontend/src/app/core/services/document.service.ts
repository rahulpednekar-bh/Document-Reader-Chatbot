import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpEventType, HttpRequest } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { DocumentMetadata, UploadDocumentResponse, UploadProgress } from '../models/document.model';

const ALLOWED_EXTENSIONS = ['.pdf', '.docx'];
const MAX_SIZE_BYTES = 25 * 1024 * 1024;

@Injectable({ providedIn: 'root' })
export class DocumentService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  validate(file: File): string | null {
    const ext = '.' + file.name.split('.').pop()?.toLowerCase();
    if (!ALLOWED_EXTENSIONS.includes(ext)) {
      return `Invalid file type. Only .pdf and .docx files are allowed.`;
    }
    if (file.size > MAX_SIZE_BYTES) {
      return `File is too large. Maximum allowed size is 25 MB.`;
    }
    return null;
  }

  upload(file: File): Observable<UploadProgress> {
    const formData = new FormData();
    formData.append('file', file);

    const req = new HttpRequest('POST', `${this.baseUrl}/documents/upload`, formData, {
      reportProgress: true
    });

    return this.http.request<UploadDocumentResponse>(req).pipe(
      map(event => {
        if (event.type === HttpEventType.UploadProgress) {
          const percent = event.total ? Math.round(100 * event.loaded / event.total) : 0;
          return { percent, status: 'uploading' as const };
        }
        if (event.type === HttpEventType.Response) {
          return { percent: 100, status: 'complete' as const };
        }
        return { percent: 0, status: 'pending' as const };
      })
    );
  }

  list(): Observable<DocumentMetadata[]> {
    return this.http.get<DocumentMetadata[]>(`${this.baseUrl}/documents`);
  }

  get(id: string): Observable<DocumentMetadata> {
    return this.http.get<DocumentMetadata>(`${this.baseUrl}/documents/${id}`);
  }

  delete(ids: string[]): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/documents`, {
      body: { documentIds: ids }
    });
  }
}
