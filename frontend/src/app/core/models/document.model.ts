export interface DocumentMetadata {
  id: string;
  fileName: string;
  blobUrl: string;
  status: 'indexed' | 'processing' | 'failed';
  uploadedAt: string;
  sizeBytes: number;
}

export interface UploadDocumentResponse {
  documentId: string;
  status: string;
  fileName: string;
}

export interface UploadProgress {
  percent: number;
  status: 'pending' | 'uploading' | 'complete' | 'error';
  error?: string;
}
