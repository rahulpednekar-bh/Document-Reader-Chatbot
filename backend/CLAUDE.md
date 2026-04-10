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
│   ├── IDocumentService / DocumentService   — blob upload + Foundry file indexing
│   ├── IChatService / ChatService           — Foundry thread/run management
│   └── ICosmosRepository / CosmosRepository — Cosmos DB generic CRUD
└── Models/
    ├── DocumentMetadata.cs    Cosmos DB document record
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

## Key Patterns

- All Azure clients use `DefaultAzureCredential`. Run `az login` locally.
- File validation (type + size) is in `DocumentService.ValidateFile` — both extensions and the 25 MB limit are enforced server-side in addition to client-side.
- The Foundry `file_search` tool handles all document parsing, chunking, embedding, and retrieval. No Azure AI Search is used.
- Cosmos DB containers use `id` as the partition key.

## IMPORTANT

When adding new Azure service clients, always register them as `AddSingleton` in `Program.cs` and inject via constructor. Never resolve from `IServiceProvider` inside functions.
