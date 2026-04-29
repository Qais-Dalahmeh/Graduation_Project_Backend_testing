import { APIRequestContext, APIResponse } from '@playwright/test';
import type { AddTransactionDto, TransactionResultDto } from './types';

export class TransactionsApi {
  constructor(
    private readonly request: APIRequestContext,
    private readonly sessionId?: string,
  ) {}

  private sessionHeaders() {
    return this.sessionId ? { 'X-Session-Id': this.sessionId } : {};
  }

  async addTransaction(body: AddTransactionDto): Promise<APIResponse> {
    return this.request.post('/api/transactions', { data: body });
  }

  async addTransactionOrFail(body: AddTransactionDto): Promise<TransactionResultDto> {
    const res = await this.addTransaction(body);
    if (!res.ok()) {
      const text = await res.text();
      throw new Error(`Add transaction failed (${res.status()}): ${text}`);
    }
    return res.json() as Promise<TransactionResultDto>;
  }

  async getTransaction(id: number): Promise<APIResponse> {
    return this.request.get(`/api/transactions/${id}`, {
      headers: this.sessionHeaders(),
    });
  }

  async getMyReceipts(params?: {
    storeId?: number;
    page?: number;
    pageSize?: number;
  }): Promise<APIResponse> {
    const searchParams = new URLSearchParams();
    if (params?.storeId) searchParams.set('storeId', String(params.storeId));
    if (params?.page) searchParams.set('page', String(params.page));
    if (params?.pageSize) searchParams.set('pageSize', String(params.pageSize));

    const qs = searchParams.toString();
    return this.request.get(
      `/api/transactions/my-receipts${qs ? `?${qs}` : ''}`,
      { headers: this.sessionHeaders() },
    );
  }
}
