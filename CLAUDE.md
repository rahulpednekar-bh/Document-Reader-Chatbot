# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

AI-powered Document Reader Chatbot — users upload PDF/DOCX documents and ask questions about their content via a chat interface.

## Sub-project Guidance

See @frontend/CLAUDE.md for Angular frontend development.
See @backend/CLAUDE.md for Azure Functions backend development.
See @docs/architecture.md for system design.
See @docs/setup-guide.md for Azure resource provisioning.

## Repository Structure

```
/
├── frontend/          Angular 17+ standalone SPA
├── backend/           .NET 8 Azure Functions API
└── docs/              Architecture and business documentation
```

## Common Commands

### Frontend
```bash
cd frontend
npm install          # install dependencies
ng serve             # dev server at http://localhost:4200
ng build --configuration production
```

### Backend
```bash
cd backend/DocumentChatbot.Functions
cp local.settings.json.example local.settings.json   # first-time setup
func start                                            # dev server at http://localhost:7071
dotnet build
dotnet restore
```

## Key Architectural Decisions

- **Azure AI Foundry Agents API** is used for all AI operations (file parsing, vectorization, chat). This eliminates the need for separate Azure AI Search or Azure AI Document Intelligence resources.
- **No authentication** — all API endpoints use `AuthorizationLevel.Anonymous`.
- **Cosmos DB** persists session metadata (session list, titles); Foundry Threads are the source of truth for message content.
- **Angular Signals** for state — no NgRx.

## Environment Variables (Backend)

All configuration lives in `local.settings.json` (local) or Azure Function App Settings (deployed). See `backend/DocumentChatbot.Functions/local.settings.json.example`.

## IMPORTANT Rules

- All service-to-service calls in Azure use `DefaultAzureCredential` (Managed Identity in Azure, `az login` locally). Never hardcode secrets.
- Document validation (type: `.pdf`/`.docx` only; size: ≤ 25 MB) must be enforced on BOTH frontend and backend.
- Follow SOLID and YAGNI: no abstractions unless they are immediately used by two or more consumers.
