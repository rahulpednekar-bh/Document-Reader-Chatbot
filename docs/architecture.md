# Architecture

## System Overview

The Document Reader Chatbot is a RAG (Retrieval Augmented Generation) system built on Azure. Users upload documents through a web interface, which are indexed by an AI agent. A chat interface then allows users to ask questions answered from document content.

**Key architectural choice:** Azure AI Foundry Agents API is used as the AI layer. The Agents API provides built-in file parsing, vectorization, semantic search (via the `file_search` tool), and thread-based conversation management — eliminating the need for custom Azure AI Search or Azure AI Document Intelligence resources.

## Components

### Frontend — Angular SPA

- Hosted on **Azure Static Web Apps**
- Two-tab layout built with Angular Material
- **Tab 1 (Document Manager):** File upload with drag-drop support, real-time progress bar, and document list with status polling
- **Tab 2 (Chat):** Chat history sidebar (left) + chat window + input (right)
- State managed with Angular Signals — no external state library
- Communicates exclusively with the Azure Functions API layer

### Backend — Azure Functions (.NET 8)

- Isolated worker process model (the current .NET model)
- Consumption plan (scales to zero when idle)
- All functions use `AuthorizationLevel.Anonymous` — no API keys required
- CORS configured to allow the Angular origin
- Two function files:
  - `DocumentFunctions` — upload, list, get status
  - `ChatFunctions` — create session, list sessions, get messages, send message
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

### Chat History

```
GET /api/sessions          → reads session list from Cosmos DB
GET /api/sessions/{id}/messages → reads Thread messages from Foundry API
```

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
