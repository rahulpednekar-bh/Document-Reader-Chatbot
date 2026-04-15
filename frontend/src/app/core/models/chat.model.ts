export interface ChatSession {
  id: string;
  title: string;
  threadId: string;
  createdAt: string;
}

export interface DocumentCitation {
  fileName: string;
  pageNumbers: number[];
}

export interface ChatMessage {
  role: 'user' | 'assistant';
  content: string;
  createdAt: string;
  citations?: DocumentCitation[];
}

export interface SendMessageResponse {
  assistantMessage: ChatMessage;
}
