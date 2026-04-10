# Document Reader Chatbot

An AI-powered chatbot that lets users upload PDF and Word documents, then ask questions about their content using natural language.

## Architecture

```
Angular SPA → Azure Functions (.NET 8) → Azure AI Foundry Agents API
                                       → Azure Blob Storage
                                       → Azure Cosmos DB
```

See [docs/architecture.md](docs/architecture.md) for full system design and [docs/architecture-diagram.md](docs/architecture-diagram.md) for the visual diagram.

## Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| Node.js | 20+ | Angular frontend |
| Angular CLI | 17+ | `npm install -g @angular/cli` |
| .NET SDK | 8.0 | Azure Functions backend |
| Azure Functions Core Tools | 4.x | `npm install -g azure-functions-core-tools@4` |
| Azure CLI | Latest | `az login` for local Managed Identity |

## Local Development

### 1. Configure Backend

```bash
cd backend/DocumentChatbot.Functions
cp local.settings.json.example local.settings.json
```

Fill in the values in `local.settings.json` — see [docs/setup-guide.md](docs/setup-guide.md) for how to get each value from the Azure Portal.

```bash
dotnet restore
func start
# API runs at http://localhost:7071
```

### 2. Configure Frontend

```bash
cd frontend
npm install
```

Update `src/environments/environment.ts` with your backend URL:
```typescript
export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:7071/api'
};
```

```bash
ng serve
# App runs at http://localhost:4200
```

### 3. Authenticate Locally

```bash
az login
# DefaultAzureCredential will use your az login session
```

## Azure Resources

The following Azure resources must be provisioned before running. See [docs/setup-guide.md](docs/setup-guide.md) for step-by-step instructions.

| Resource | Purpose |
|----------|---------|
| Azure AI Foundry Project | Hosts the Agent, Vector Store, Files, and Threads |
| Azure Blob Storage | Stores raw uploaded documents |
| Azure Cosmos DB | Persists chat session and document metadata |
| Azure Functions (Consumption) | Hosts the API backend |
| Azure Static Web Apps | Hosts the Angular SPA |

## Deployment

- **Backend**: Deploy `backend/DocumentChatbot.Functions` to Azure Functions using `func azure functionapp publish <app-name>` or GitHub Actions.
- **Frontend**: Push to the linked GitHub repo — Azure Static Web Apps builds and deploys automatically.

## Documentation

- [Business Requirements](docs/business-requirements.md)
- [Architecture](docs/architecture.md)
- [Architecture Diagram](docs/architecture-diagram.md)
- [Azure Setup Guide](docs/setup-guide.md)
