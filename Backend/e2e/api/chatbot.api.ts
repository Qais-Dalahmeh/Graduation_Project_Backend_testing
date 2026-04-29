import { APIRequestContext, APIResponse } from '@playwright/test';

export class ChatbotApi {
  constructor(private readonly request: APIRequestContext) {}

  async ask(message: string, conversationSessionId?: string): Promise<APIResponse> {
    return this.request.post('/api/Chatbot/ask', {
      data: {
        message,
        ...(conversationSessionId ? { conversationSessionId } : {}),
      },
    });
  }

  async askWithMsgField(msg: string): Promise<APIResponse> {
    return this.request.post('/api/Chatbot/ask', { data: { msg } });
  }

  async askWithQuestionField(question: string): Promise<APIResponse> {
    return this.request.post('/api/Chatbot/ask', { data: { question } });
  }

  async getHistory(): Promise<APIResponse> {
    return this.request.get('/api/Chatbot/history');
  }
}
