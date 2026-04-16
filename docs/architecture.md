# Architecture

## System Overview

The Document Reader Chatbot is a RAG (Retrieval Augmented Generation) system built on Azure. Users upload documents through a web interface, which are indexed by an AI agent. A chat interface then allows users to ask questions answered from document content.

**Key architectural choice:** Azure AI Foundry Agents API is used as the AI layer. The Agents API provides built-in file parsing, vectorization, semantic search (via the `file_search` tool), and thread-based conversation management — eliminating the need for custom Azure AI Search resources.

**Scanned PDF support:** Azure AI Foundry's `file_search` tool only reads embedded text layers from PDFs. Scanned PDFs (image-only) contain no text layer, so Foundry would index nothing. To handle this, the backend runs an automatic OCR pipeline using **Azure AI Document Intelligence** (`prebuilt-read` model) whenever a scanned PDF is detected, extracting text before the file reaches Foundry.

## Components

### Frontend — Angular SPA

- Hosted on **Azure Static Web Apps**
- Two-tab layout built with Angular Material
- **Tab 1 (Document Manager):** File upload with drag-drop support, real-time progress bar, document list with multi-select checkboxes, and bulk delete with confirmation
  - Documents processed via OCR show an **"OCR Applied"** badge with a tooltip
  - Documents that failed processing show a **"Failed"** badge with a tooltip explaining the reason
- **Tab 2 (Chat):** Chat history sidebar (left) + chat window + input (right)
- State managed with Angular Signals — no external state library
- Communicates exclusively with the Azure Functions API layer

### Backend — Azure Functions (.NET 8)

- Isolated worker process model (the current .NET model)
- Consumption plan (scales to zero when idle)
- All functions use `AuthorizationLevel.Anonymous` — no API keys required
- CORS configured to allow the Angular origin
- Two function files:
  - `DocumentFunctions` — upload, list, get status, bulk delete
  - `ChatFunctions` — create session, list sessions, get messages, send message, delete session
- Three service files relevant to document upload:
  - `DocumentService` — orchestrates blob upload, OCR detection, and Foundry indexing
  - `OcrService` — scanned PDF detection (PdfPig) and OCR extraction (Document Intelligence)
  - `CosmosRepository` — generic Cosmos DB CRUD
- Dependency injection via `Program.cs` using `HostBuilder`
- All Azure service clients authenticated via `DefaultAzureCredential`

### Azure AI Document Intelligence

- Used exclusively for **OCR of scanned PDFs**
- Model: `prebuilt-read` — layout-aware OCR across all pages, returns page/line structured text
- Only invoked when `OcrService.IsPdfScanned()` returns `true` for a `.pdf` upload
- The extracted plain-text content (a `.txt` file) is what gets uploaded to Foundry — not the original scanned PDF
- DOCX files are never routed through OCR (DOCX is always XML-based with embedded text)

### Azure AI Foundry Agents API

- Manages the AI **Agent** (configured with `file_search` tool and a system prompt)
- **Files API** — stores uploaded documents in Foundry's internal file store
- **Vector Store** — Foundry automatically parses, chunks, and embeds files attached to a Vector Store
- **Threads API** — each chat session maps to one Foundry Thread
- **Runs API** — each user message triggers a Run on the Thread using the Agent; Foundry handles retrieval and completion

### Azure Blob Storage

- Stores the **original** raw uploaded files for management purposes (listing, metadata, audit)
- Scanned PDFs are stored as uploaded — the OCR output is what is sent to Foundry, but the original scan is preserved in Blob
- One container: `documents` (private)

### Azure Cosmos DB (NoSQL)

- Two containers:
  - `documents` — document metadata: `{id, fileName, blobUrl, foundryFileId, status, uploadedAt, sizeBytes, ocrApplied, processingNote}`
  - `sessions` — chat session metadata: `{id, title, threadId, createdAt}`
- `ocrApplied` (bool) — `true` when the document was a scanned PDF processed through OCR before indexing
- `processingNote` (string, nullable) — human-readable note; populated when OCR is applied or when upload fails
- Session message content is retrieved on demand from Foundry Threads, not stored redundantly in Cosmos DB

## Data Flows

### Document Upload — Text-Based PDF or DOCX

```
1. User selects file in Angular (validates: type = .pdf/.docx, size ≤ 25 MB)
2. Angular POST /api/documents/upload (multipart/form-data)
3. Function:
   a. Re-validates file server-side
   b. Uploads stream to Azure Blob Storage → gets blobUrl
   c. [PDF only] OcrService.IsPdfScanned() → false (text layer present)
   d. Uploads original file stream to Azure AI Foundry Files API → gets foundryFileId
   e. Attaches foundryFileId to the shared Vector Store
   f. Saves DocumentMetadata to Cosmos DB (status="indexed", ocrApplied=false)
   g. Returns { documentId, status, ocrApplied: false }
4. Angular updates document list; status badge shows "Ready"
```

### Document Upload — Scanned PDF

```
1. User selects scanned PDF in Angular (passes validation: it is .pdf, ≤ 25 MB)
2. Angular POST /api/documents/upload (multipart/form-data)
3. Function:
   a. Re-validates file server-side
   b. Uploads original scan to Azure Blob Storage → gets blobUrl (original preserved)
   c. OcrService.IsPdfScanned() → true (PdfPig finds < 50 chars in first 5 pages)
   d. OcrService.ExtractTextAsync() → calls Document Intelligence prebuilt-read model
      - Document Intelligence performs OCR across all pages
      - Returns page/line structured text
      - OcrService assembles a UTF-8 plain-text MemoryStream
   e. Uploads the OCR text stream (as {fileName}_ocr.txt) to Foundry Files API
   f. Attaches foundryFileId to the shared Vector Store
   g. Saves DocumentMetadata to Cosmos DB (status="indexed", ocrApplied=true,
      processingNote="Scanned PDF detected. Text was extracted via OCR before indexing.")
   h. Returns { documentId, status, ocrApplied: true, processingNote }
4. Angular updates document list; shows "Ready" badge + blue "OCR Applied" badge
```

### Document Upload — OCR Failure

```
1–3c. Same as scanned PDF flow above.
   d. OcrService.ExtractTextAsync() throws (e.g. Document Intelligence quota exceeded,
      network timeout, invalid PDF bytes)
   e. DocumentService catches exception:
      - Saves DocumentMetadata to Cosmos DB (status="failed",
        processingNote="OCR processing failed: {exception message}")
      - Throws InvalidOperationException with user-readable message
   f. DocumentFunctions catches InvalidOperationException →
      returns HTTP 422 { error: "...", code: "ocr_failed" }
4. Angular shows error in progress bar:
   "OCR processing failed for this scanned PDF. Please try again or use a text-based PDF."
```

### Chat Session

```
1. User clicks "New Chat" → Angular POST /api/sessions
2. Function creates a Foundry Thread → stores { id, threadId, title, createdAt } in Cosmos DB
3. User types a message → Angular POST /api/sessions/{id}/messages
4. Function:
   a. Adds user message to the Foundry Thread
   b. Creates a Run (Agent + Vector Store attached)
   c. Polls Run status until terminal state (completed/failed)
   d. Reads the assistant message from the Thread
   e. Returns { role: "assistant", content: "..." }
5. Angular renders the response (with markdown support)
```

### Session Deletion

```
1. User hovers over a session row in the sidebar → trash icon appears
2. User clicks the icon → browser confirm dialog warns deletion is permanent
3. Angular DELETE /api/sessions/{id}
4. Function (ChatFunctions.DeleteSession):
   a. Reads ChatSession from Cosmos DB (no-op if not found)
   b. Calls AgentsClient.DeleteThreadAsync → removes the Foundry Thread and all messages
   c. Deletes ChatSession from Cosmos DB
5. Angular removes the session from the list
   - If the deleted session was active, clears the chat window and shows the empty state
```

### Document Deletion

```
1. User selects one or more documents via checkboxes in the document list
   (individual row click or "Select all" header checkbox)
2. "Delete (N)" button appears in the list header; disabled when nothing is selected
3. User clicks Delete → browser confirm dialog warns deletion is permanent
4. Angular DELETE /api/documents  { "documentIds": ["id1", "id2", ...] }
5. Function (DocumentFunctions.DeleteDocuments) iterates each ID:
   a. Reads DocumentMetadata from Cosmos DB (skips if not found)
   b. Calls Foundry AgentsClient.DeleteVectorStoreFileAsync → removes file from Vector Store
   c. Calls Foundry AgentsClient.DeleteFileAsync → removes file from Foundry Files API
      (For OCR-processed documents this is the _ocr.txt file that was indexed)
   d. Calls BlobContainerClient.DeleteIfExistsAsync → removes original file from Blob Storage
   e. Deletes DocumentMetadata from Cosmos DB (last, so record survives if earlier steps fail)
6. Angular clears the selection and reloads the document list
```

**Deletion order rationale:** Cosmos DB is deleted last so the document record remains
visible if an earlier step (Foundry/Blob) fails — the user can retry deletion.
Steps (b) and (c) are individually wrapped in try-catch: if a Foundry file was already
removed, cleanup continues rather than aborting.

### Chat History

```
GET /api/sessions          → reads session list from Cosmos DB
GET /api/sessions/{id}/messages → reads Thread messages from Foundry API
```

### Document Citations

Each assistant message returned by `GET /api/sessions/{id}/messages` and `POST /api/sessions/{id}/messages` includes a `citations` array with the source documents the Agent referenced:

```
citations: [
  { fileName: "report.pdf", pageNumbers: [3, 5] },
  { fileName: "manual.docx", pageNumbers: [] }
]
```

**Note:** For scanned PDFs processed via OCR, citations will reference the generated `_ocr.txt` file name (e.g. `scan_ocr.txt`) since that is the file indexed in Foundry.

**Extraction process (backend):**
1. Each `MessageTextContent` exposes an `Annotations` list containing `MessageTextFileCitationAnnotation` items
2. Annotations are deduplicated by `FileId` (same file cited multiple times → one citation entry)
3. `AgentsClient.GetFileAsync(fileId)` resolves the file name
4. Page numbers are extracted by regex from `FileCitation.Quote` (matches "page N" / "pages N–M" patterns); empty list if no pages mentioned
5. Foundry inline markers (e.g. `【4:0†source】`) are stripped from the response text before returning

**Display (frontend):** A "Sources:" row with Material chips appears below each assistant message bubble when `citations.length > 0`. Each chip shows the document icon, file name, and optional page numbers.

## Security

- All backend-to-Azure-service calls use Managed Identity (`DefaultAzureCredential`) — no secrets in code or config
- The Document Intelligence client uses `AzureKeyCredential` locally (key from `local.settings.json`); in production, switch to `DefaultAzureCredential` with `Cognitive Services User` role on the Managed Identity
- Blob container is private — no public access
- Frontend origin is whitelisted in Function CORS configuration
- File type and size validation enforced on both client and server

## Deployment Targets

| Component | Azure Service |
|-----------|--------------|
| Angular SPA | Azure Static Web Apps |
| API | Azure Functions (Consumption) |
| AI Agent | Azure AI Foundry |
| OCR (scanned PDFs) | Azure AI Document Intelligence |
| Documents | Azure Blob Storage |
| Metadata | Azure Cosmos DB (NoSQL) |
