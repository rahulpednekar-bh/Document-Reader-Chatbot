# CLAUDE.md — Frontend

Angular 17+ standalone SPA for the Document Reader Chatbot.

## Commands

```bash
# From frontend/
npm install
ng serve          # dev server at http://localhost:4200 (proxies /api to localhost:7071)
ng build          # production build → dist/frontend/
ng build --configuration development   # dev build with source maps
```

## Project Structure

```
src/app/
├── app.component.ts              Root — MatTabGroup with Documents and Chat tabs
├── app.config.ts                 Providers: HttpClient, Animations, Router
├── features/
│   ├── document-manager/
│   │   ├── document-manager.component.ts   Holds documents signal, coordinates children
│   │   ├── file-upload/                    Drag-drop, validation, progress bar
│   │   └── document-list/                  Document list with status chips
│   └── chat/
│       ├── chat.component.ts               Holds sessions/messages signals, coordinates children
│       ├── chat-history-sidebar/           Session list + New Chat button
│       ├── chat-window/                    Message bubbles + markdown rendering
│       └── chat-input/                     Textarea, Enter-to-send, Shift+Enter = newline
└── core/
    ├── services/
    │   ├── document.service.ts    validate(), upload(), list(), get()
    │   └── chat.service.ts        createSession(), getSessions(), getMessages(), sendMessage()
    └── models/
        ├── document.model.ts      DocumentMetadata, UploadProgress
        └── chat.model.ts          ChatSession, ChatMessage
```

## Key Patterns

- **State:** Angular Signals only — no NgRx. Each feature component owns its `signal<T>()` state.
- **HTTP:** All calls go through `HttpClient` to `/api/*`, which is proxied to the backend in dev via `proxy.conf.json`.
- **File upload:** Uses `HttpRequest` with `reportProgress: true` to stream `UploadProgress` events.
- **Markdown:** Assistant responses rendered with `marked` + sanitized with `DOMPurify` before using `bypassSecurityTrustHtml`.
- **Validation:** Client-side validation in `DocumentService.validate()` mirrors server-side rules.

## Environment

- `src/environments/environment.ts` — dev (`apiBaseUrl: '/api'`)
- `src/environments/environment.prod.ts` — prod (same default; override in Azure Static Web Apps config)

## Angular Material Theme

Uses the `azure-blue` pre-built theme (`@angular/material/prebuilt-themes/azure-blue.css`).
