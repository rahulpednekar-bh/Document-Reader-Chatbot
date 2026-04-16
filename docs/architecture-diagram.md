# Architecture Diagram

## Full System Diagram

```mermaid
graph TB
    subgraph Browser["Browser"]
        FE["Angular SPA<br/>(Azure Static Web Apps)"]
        Tab1["Tab 1: Document Manager<br/>- FileUploadComponent<br/>- DocumentListComponent"]
        Tab2["Tab 2: Chat<br/>- ChatHistorySidebar<br/>- ChatWindow<br/>- ChatInput"]
        FE --> Tab1
        FE --> Tab2
    end

    subgraph Functions["Azure Functions (.NET 8 Isolated)"]
        DF["DocumentFunctions<br/>POST /api/documents/upload<br/>GET  /api/documents<br/>GET  /api/documents/{id}"]
        CF["ChatFunctions<br/>POST /api/sessions<br/>GET  /api/sessions<br/>GET  /api/sessions/{id}/messages<br/>POST /api/sessions/{id}/messages"]
        DS["DocumentService<br/>+ OcrService"]
    end

    subgraph AzureServices["Azure Services"]
        Blob["Azure Blob Storage<br/>Container: documents<br/>(original raw files)"]
        Cosmos["Azure Cosmos DB<br/>Container: documents<br/>Container: sessions"]
        DocIntel["Azure AI Document Intelligence<br/>prebuilt-read model<br/>(OCR for scanned PDFs)"]
        Foundry["Azure AI Foundry<br/>Agents API<br/>- Agent (gpt-4o-mini)<br/>- Files API<br/>- Vector Store<br/>- Threads API<br/>- Runs API"]
    end

    Tab1 -- "multipart upload" --> DF
    Tab1 -- "poll status" --> DF
    Tab2 -- "create/list sessions" --> CF
    Tab2 -- "send/get messages" --> CF

    DF --> DS
    DS -- "store original file" --> Blob
    DS -- "scanned PDF only: OCR" --> DocIntel
    DS -- "upload file (text or OCR output) + attach to Vector Store" --> Foundry
    DS -- "save document metadata" --> Cosmos

    CF -- "create Thread" --> Foundry
    CF -- "add message + create Run" --> Foundry
    CF -- "list Thread messages" --> Foundry
    CF -- "save/list session metadata" --> Cosmos

    style Browser fill:#dbeafe,stroke:#3b82f6
    style Functions fill:#dcfce7,stroke:#22c55e
    style AzureServices fill:#fef9c3,stroke:#eab308
```

## Document Upload Sequence — Text-Based PDF / DOCX

```mermaid
sequenceDiagram
    participant U as User (Browser)
    participant FE as Angular
    participant FN as Azure Functions
    participant Blob as Blob Storage
    participant Foundry as AI Foundry
    participant Cosmos as Cosmos DB

    U->>FE: Select file (.pdf/.docx, ≤25MB)
    FE->>FE: Validate type & size (client-side)
    FE->>FN: POST /api/documents/upload (multipart)
    FN->>FN: Validate type & size (server-side)
    FN->>Blob: Upload original file stream
    Blob-->>FN: blobUrl
    FN->>FN: IsPdfScanned() → false (text layer present)
    FN->>Foundry: Upload original file (Files API)
    Foundry-->>FN: foundryFileId
    FN->>Foundry: Attach file to Vector Store
    FN->>Cosmos: Save DocumentMetadata {status:"indexed", ocrApplied:false}
    FN-->>FE: {documentId, status:"indexed", ocrApplied:false}
    FE->>FE: Show "Ready" status badge
```

## Document Upload Sequence — Scanned PDF (Auto-OCR)

```mermaid
sequenceDiagram
    participant U as User (Browser)
    participant FE as Angular
    participant FN as Azure Functions
    participant Blob as Blob Storage
    participant DI as Document Intelligence
    participant Foundry as AI Foundry
    participant Cosmos as Cosmos DB

    U->>FE: Select scanned PDF (≤25MB)
    FE->>FE: Validate type & size (client-side — passes)
    FE->>FN: POST /api/documents/upload (multipart)
    FN->>FN: Validate type & size (server-side)
    FN->>Blob: Upload original scan (preserved for audit)
    Blob-->>FN: blobUrl
    FN->>FN: IsPdfScanned() via PdfPig → true (< 50 chars)
    FN->>DI: AnalyzeDocument (prebuilt-read model)
    DI-->>FN: Page/line OCR text
    FN->>FN: Assemble plain-text stream ({fileName}_ocr.txt)
    FN->>Foundry: Upload OCR text file (Files API)
    Foundry-->>FN: foundryFileId
    FN->>Foundry: Attach OCR file to Vector Store
    FN->>Cosmos: Save DocumentMetadata {status:"indexed", ocrApplied:true}
    FN-->>FE: {documentId, status:"indexed", ocrApplied:true}
    FE->>FE: Show "Ready" badge + blue "OCR Applied" badge
```

## Document Upload Sequence — OCR Failure

```mermaid
sequenceDiagram
    participant U as User (Browser)
    participant FE as Angular
    participant FN as Azure Functions
    participant Blob as Blob Storage
    participant DI as Document Intelligence
    participant Cosmos as Cosmos DB

    U->>FE: Select scanned PDF
    FE->>FN: POST /api/documents/upload (multipart)
    FN->>Blob: Upload original scan
    Blob-->>FN: blobUrl
    FN->>FN: IsPdfScanned() → true
    FN->>DI: AnalyzeDocument (prebuilt-read)
    DI-->>FN: Error (timeout / quota / invalid file)
    FN->>Cosmos: Save DocumentMetadata {status:"failed", processingNote:"..."}
    FN-->>FE: HTTP 422 {error:"...", code:"ocr_failed"}
    FE->>FE: Show error: "OCR processing failed..."
```

## Chat Session Sequence

```mermaid
sequenceDiagram
    participant U as User (Browser)
    participant FE as Angular
    participant FN as Azure Functions
    participant Foundry as AI Foundry
    participant Cosmos as Cosmos DB

    U->>FE: Click "New Chat"
    FE->>FN: POST /api/sessions
    FN->>Foundry: Create Thread
    Foundry-->>FN: threadId
    FN->>Cosmos: Save session {id, threadId, title, createdAt}
    FN-->>FE: {sessionId}

    U->>FE: Type question + press Enter
    FE->>FN: POST /api/sessions/{id}/messages {content}
    FN->>Foundry: Add user message to Thread
    FN->>Foundry: Create Run (Agent + Vector Store)
    loop Poll until complete
        FN->>Foundry: Get Run status
        Foundry-->>FN: status
    end
    FN->>Foundry: List Thread messages
    Foundry-->>FN: messages (including assistant response)
    FN-->>FE: {role:"assistant", content:"..."}
    FE->>FE: Render markdown response
```
