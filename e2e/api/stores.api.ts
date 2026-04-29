import { APIRequestContext, APIResponse } from '@playwright/test';
import type { CreateOfferRequest } from './types';

export class StoresApi {
  constructor(
    private readonly request: APIRequestContext,
    private readonly sessionId?: string,
  ) {}

  private sessionHeaders() {
    return this.sessionId ? { 'X-Session-Id': this.sessionId } : {};
  }

  async list(): Promise<APIResponse> {
    return this.request.get('/api/stores', {
      headers: this.sessionHeaders(),
    });
  }

  async getById(id: string): Promise<APIResponse> {
    return this.request.get(`/api/stores/${id}`, {
      headers: this.sessionHeaders(),
    });
  }

  async getManagedStores(): Promise<APIResponse> {
    return this.request.get('/api/stores/manage', {
      headers: this.sessionHeaders(),
    });
  }
}

export class OffersApi {
  constructor(
    private readonly request: APIRequestContext,
    private readonly sessionId?: string,
  ) {}

  private sessionHeaders() {
    return this.sessionId ? { 'X-Session-Id': this.sessionId } : {};
  }

  async list(): Promise<APIResponse> {
    return this.request.get('/api/offers', {
      headers: this.sessionHeaders(),
    });
  }

  async create(body: CreateOfferRequest): Promise<APIResponse> {
    return this.request.post('/api/offers', {
      data: body,
      headers: this.sessionHeaders(),
    });
  }

  async update(id: number, body: Partial<CreateOfferRequest>): Promise<APIResponse> {
    return this.request.put(`/api/offers/${id}`, {
      data: body,
      headers: this.sessionHeaders(),
    });
  }

  async delete(id: number): Promise<APIResponse> {
    return this.request.delete(`/api/offers/${id}`, {
      headers: this.sessionHeaders(),
    });
  }

  async setStatus(id: number, isActive: boolean): Promise<APIResponse> {
    return this.request.patch(`/api/offers/${id}/status`, {
      data: { isActive },
      headers: this.sessionHeaders(),
    });
  }

  async getManagedOffers(): Promise<APIResponse> {
    return this.request.get('/api/offers/manage', {
      headers: this.sessionHeaders(),
    });
  }
}
