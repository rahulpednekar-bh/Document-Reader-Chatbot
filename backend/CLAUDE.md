# CLAUDE.md — Backend

Azure Functions (.NET 8 isolated worker) API for the Document Reader Chatbot.

## Commands

```bash
# From backend/DocumentChatbot.Functions/
dotnet restore
dotnet build
func start                    # dev server at http://localhost:7071
```

## Project Structure

```
DocumentChatbot.Functions/
├── Program.cs                 DI registration and host configuration
├── Functions/
│   ├── DocumentFunctions.cs   POST /api/documents/upload, GET /api/documents, GET /api/documents/{id}
│   └── ChatFunctions.cs       POST /api/sessions, GET /api/sessions,
│                              GET /api/sessions/{id}/messages, POST /api/sessions/{id}/messages
├── Services/
│   ├── IDocumentService / DocumentService   — blob upload, OCR detection, Foundry file indexing
│   ├── IOcrService / OcrService             — scanned PDF detection (PdfPig) + OCR extraction (Document Intelligence)
│   ├── IChatService / ChatService           — Foundry thread/run management
│   └── ICosmosRepository / CosmosRepository — Cosmos DB generic CRUD
└── Models/
    ├── DocumentMetadata.cs    Cosmos DB document record (includes ocrApplied, processingNote)
    ├── ChatSession.cs         Cosmos DB session record
    └── ApiModels.cs           Request/response DTOs
```

## Environment Variables

See `local.settings.json.example`. Required keys:

| Key | Description |
|-----|-------------|
| `AzureFoundry__ConnectionString` | AI Foundry project connection string |
| `AzureFoundry__AgentId` | ID of the configured Foundry Agent |
| `AzureFoundry__VectorStoreId` | ID of the Foundry Vector Store |
| `BlobStorage__ConnectionString` | Azure Storage connection string |
| `BlobStorage__ContainerName` | Blob container name (default: `documents`) |
| `CosmosDB__ConnectionString` | Cosmos DB connection string |
| `CosmosDB__DatabaseName` | Database name (default: `docreader`) |
| `AzureDocumentIntelligence__Endpoint` | Document Intelligence resource endpoint (e.g. `https://<name>.cognitiveservices.azure.com/`) |
| `AzureDocumentIntelligence__Key` | Document Intelligence API key — **local dev only**; production uses Managed Identity |

## Key Patterns

- All Azure clients use `DefaultAzureCredential`. Run `az login` locally.
- The `DocumentIntelligenceClient` uses `AzureKeyCredential` locally (key from `local.settings.json`). In production, replace with `new DefaultAzureCredential()` and assign the `Cognitive Services User` role to the Function App's Managed Identity on the Document Intelligence resource.
- File validation (type + size) is in `DocumentService.ValidateFile` — both extensions and the 25 MB limit are enforced server-side in addition to client-side.
- `OcrService.IsPdfScanned()` uses **PdfPig** to sample text from the first 5 pages. If total character count < 50, the PDF is treated as a scanned image.
- `OcrService.ExtractTextAsync()` calls the **Azure AI Document Intelligence** `prebuilt-read` model and assembles the structured page/line output into a UTF-8 plain-text stream uploaded to Foundry as `{fileName}_ocr.txt`.
- The original scanned PDF is always preserved in Blob Storage. Only the OCR output is sent to Foundry.
- `DocumentFunctions.UploadAsync` returns HTTP 422 with `{ code: "ocr_failed" }` when OCR throws.
- Cosmos DB containers use `id` as the partition key.

## IMPORTANT

When adding new Azure service clients, always register them as `AddSingleton` in `Program.cs` and inject via constructor. Never resolve from `IServiceProvider` inside functions.
