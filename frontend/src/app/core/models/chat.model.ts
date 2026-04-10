export interface ChatSession {
  id: string;
  title: string;
  threadId: string;
  createdAt: string;
}

export interface ChatMessage {
  role: 'user' | 'assistant';
  content: string;
  createdAt: string;
}

export interface SendMessageResponse {
  assistantMessage: ChatMessage;
}
