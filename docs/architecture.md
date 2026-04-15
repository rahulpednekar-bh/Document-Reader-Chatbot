# Architecture

## System Overview

The Document Reader Chatbot is a RAG (Retrieval Augmented Generation) system built on Azure. Users upload documents through a web interface, which are indexed by an AI agent. A chat interface then allows users to ask questions answered from document content.

**Key architectural choice:** Azure AI Foundry Agents API is used as the AI layer. The Agents API provides built-in file parsing, vectorization, semantic search (via the `file_search` tool), and thread-based conversation management — eliminating the need for custom Azure AI Search or Azure AI Document Intelligence resources.

## Components

### Frontend — Angular SPA

- Hosted on **Azure Static Web Apps**
- Two-tab layout built with Angular Material
- **Tab 1 (Document Manager):** File upload with drag-drop support, real-time progress bar, document list with multi-select checkboxes, and bulk delete with confirmation
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
- Dependency injection via `Program.cs` using `HostBuilder`
- All Azure service clients authenticated via `DefaultAzureCredential`

### Azure AI Foundry Agents API

- Manages the AI **Agent** (configured with `file_search` tool and a system prompt)
- **Files API** — stores uploaded documents in Foundry's internal file store
- **Vector Store** — Foundry automatically parses, chunks, and embeds files attached to a Vector Store
- **Threads API** — each chat session maps to one Foundry Thread
- **Runs API** — each user message triggers a Run on the Thread using the Agent; Foundry handles retrieval and completion

### Azure Blob Storage

- Stores the raw uploaded files for management purposes (listing, metadata)
- One container: `documents` (private)
- Blob metadata records: file name, size, upload timestamp, status, and the corresponding Foundry File ID

### Azure Cosmos DB (NoSQL)

- Two containers:
  - `documents` — document metadata: `{id, fileName, blobUrl, foundryFileId, status, uploadedAt, sizeBytes}`
  - `sessions` — chat session metadata: `{id, title, threadId, createdAt}`
- Session message content is retrieved on demand from Foundry Threads, not stored redundantly in Cosmos DB

## Data Flows

### Document Upload

```
1. User selects file in Angular (validates: type = .pdf/.docx, size ≤ 25 MB)
2. Angular POST /api/documents/upload (multipart/form-data)
3. Function:
   a. Re-validates file server-side
   b. Uploads stream to Azure Blob Storage → gets blobUrl
   c. Uploads file to Azure AI Foundry Files API → gets foundryFileId
   d. Attaches foundryFileId to the shared Vector Store
   e. Saves DocumentMetadata to Cosmos DB (status = "indexed")
   f. Returns { documentId, status }
4. Angular updates document list; status badge shows "Ready"
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
   d. Calls BlobContainerClient.DeleteIfExistsAsync → removes raw file from Blob Storage
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

**Extraction process (backend):**
1. Each `MessageTextContent` exposes an `Annotations` list containing `MessageTextFileCitationAnnotation` items
2. Annotations are deduplicated by `FileId` (same file cited multiple times → one citation entry)
3. `AgentsClient.GetFileAsync(fileId)` resolves the file name
4. Page numbers are extracted by regex from `FileCitation.Quote` (matches "page N" / "pages N–M" patterns); empty list if no pages mentioned
5. Foundry inline markers (e.g. `【4:0†source】`) are stripped from the response text before returning

**Display (frontend):** A "Sources:" row with Material chips appears below each assistant message bubble when `citations.length > 0`. Each chip shows the document icon, file name, and optional page numbers.

## Security

- All backend-to-Azure-service calls use Managed Identity (`DefaultAzureCredential`) — no secrets in code or config
- Blob container is private — no public access
- Frontend origin is whitelisted in Function CORS configuration
- File type and size validation enforced on both client and server

## Deployment Targets

| Component | Azure Service |
|-----------|--------------|
| Angular SPA | Azure Static Web Apps |
| API | Azure Functions (Consumption) |
| AI Agent | Azure AI Foundry |
| Documents | Azure Blob Storage |
| Metadata | Azure Cosmos DB (NoSQL) |
