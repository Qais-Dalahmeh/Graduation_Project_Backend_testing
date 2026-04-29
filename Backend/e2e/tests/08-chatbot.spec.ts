import { test, expect } from '../fixtures/base-test';
import { randomUUID } from 'crypto';

async function isChatbotConfigured(chatbotApi: import('../api/chatbot.api').ChatbotApi): Promise<boolean> {
  const res = await chatbotApi.ask('ping');
  if (res.status() === 200) return true;
  const body = await res.json().catch(() => ({}));
  return body?.error?.code !== 'CHATBOT_SETTING_MISSING';
}

test.describe('Chatbot — FAQ Assistant', () => {
  test('chatbot endpoint is reachable without a session', async ({ chatbotApi }) => {
    const res = await chatbotApi.ask('Hello');
    expect(res.status()).toBeLessThan(500);
  });

  test('chatbot returns a structured response (not a crash)', async ({ chatbotApi }) => {
    const res = await chatbotApi.ask('What are the mall hours?');
    expect(res.status()).toBeLessThan(500);
    const body = await res.json();
    const hasAnswer = 'botResponse' in body;
    const hasError = body?.error?.code === 'CHATBOT_SETTING_MISSING';
    expect(hasAnswer || hasError).toBe(true);
  });

  test('chatbot responds with answer when configured', async ({ chatbotApi }) => {
    if (!(await isChatbotConfigured(chatbotApi))) {
      test.skip(true, 'Chatbot not configured (AI_API_KEY missing) — skipping on local');
      return;
    }

    const res = await chatbotApi.ask('What are the mall operating hours?');
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('botResponse');
    expect(typeof body.botResponse).toBe('string');
    expect(body.botResponse.length).toBeGreaterThan(0);
  });

  test('chatbot accepts "msg" field as alternative', async ({ chatbotApi }) => {
    if (!(await isChatbotConfigured(chatbotApi))) {
      test.skip(true, 'Chatbot not configured — skipping on local');
      return;
    }
    const res = await chatbotApi.askWithMsgField('What stores are available?');
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('botResponse');
  });

  test('chatbot accepts "question" field as alternative', async ({ chatbotApi }) => {
    if (!(await isChatbotConfigured(chatbotApi))) {
      test.skip(true, 'Chatbot not configured — skipping on local');
      return;
    }
    const res = await chatbotApi.askWithQuestionField('How do I earn points?');
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('botResponse');
  });

  test('chatbot response includes responseTimeMs', async ({ chatbotApi }) => {
    if (!(await isChatbotConfigured(chatbotApi))) {
      test.skip(true, 'Chatbot not configured — skipping on local');
      return;
    }
    const res = await chatbotApi.ask('How do I redeem a coupon?');
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(typeof body.responseTimeMs).toBe('number');
    expect(body.responseTimeMs).toBeGreaterThanOrEqual(0);
  });

  test('multiple questions in same conversation session are accepted', async ({
    chatbotApi,
  }) => {
    if (!(await isChatbotConfigured(chatbotApi))) {
      test.skip(true, 'Chatbot not configured — skipping on local');
      return;
    }
    const sessionId = randomUUID();
    const first = await chatbotApi.ask('What is the loyalty program?', sessionId);
    expect(first.status()).toBe(200);
    const second = await chatbotApi.ask('How many points per purchase?', sessionId);
    expect(second.status()).toBe(200);
    expect(await second.json()).toHaveProperty('botResponse');
  });

  test('chatbot history endpoint returns 200', async ({ chatbotApi }) => {
    const res = await chatbotApi.getHistory();
    expect(res.status()).toBe(200);
    expect(Array.isArray(await res.json())).toBe(true);
  });
});
