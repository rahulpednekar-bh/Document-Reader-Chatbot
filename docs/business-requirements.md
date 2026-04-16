# Business Requirements

## Overview

A web-based AI chatbot system that enables users to upload business documents and interact with an AI agent to retrieve information from those documents via natural language questions.

## Functional Requirements

### FR-01: Document Upload (Tab 1)

| ID | Requirement |
|----|-------------|
| FR-01-1 | Users can upload documents from a dedicated upload tab |
| FR-01-2 | Supported formats: PDF (`.pdf`) and Word (`.docx`) only |
| FR-01-3 | Maximum file size per document: 25 MB |
| FR-01-4 | A progress bar must display upload progress in real time |
| FR-01-5 | Invalid file types must be rejected with a clear error message |
| FR-01-6 | Files exceeding 25 MB must be rejected before upload begins |
| FR-01-7 | Uploaded documents are listed with their processing status (Pending → Processing → Ready) |
| FR-01-8 | Scanned PDFs (image-only, no embedded text layer) must be automatically processed with OCR before indexing, without requiring any action from the user |
| FR-01-9 | Documents that were processed via OCR must be visually distinguished in the document list with an "OCR Applied" badge |
| FR-01-10 | If OCR processing fails, the document must be marked as "Failed" with a descriptive error message visible to the user |

### FR-02: AI Chat Interface (Tab 2)

| ID | Requirement |
|----|-------------|
| FR-02-1 | Users interact with an AI agent through a chat interface on a dedicated tab |
| FR-02-2 | The AI responds based exclusively on the content of uploaded documents |
| FR-02-3 | All previous chat sessions are listed in a sidebar on the left side of the chat tab |
| FR-02-4 | Users can click any previous session to view its full message history |
| FR-02-5 | Users can start a new chat session at any time |
| FR-02-6 | Chat history persists across browser sessions |

## Non-Functional Requirements

| ID | Requirement |
|----|-------------|
| NFR-01 | The system is deployed to Microsoft Azure |
| NFR-02 | No user authentication or login is required |
| NFR-03 | The frontend must be developed in Angular with TypeScript |
| NFR-04 | The backend API must be implemented as Azure Functions in .NET C# |
| NFR-05 | The AI agent must be provisioned through Azure AI Foundry |
| NFR-06 | Document storage must use Azure Blob Storage |
| NFR-07 | All dependencies must be open source |
| NFR-08 | Code must follow SOLID and YAGNI principles |
| NFR-09 | OCR processing for scanned PDFs must use Azure AI Document Intelligence (`prebuilt-read` model) |

## Constraints

- Documents are limited to PDF and DOCX formats only
- Individual document size may not exceed 25 MB
- Chat responses must be grounded in uploaded document content only
- Scanned PDFs are supported via automatic OCR; the original file is always preserved in Blob Storage
