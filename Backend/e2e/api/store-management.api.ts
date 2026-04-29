import { APIRequestContext, APIResponse } from '@playwright/test';

export interface CreateStoreBody {
  name: string;
  operatingHours?: string;
  description?: string;
  phoneNumber?: string;
  email?: string;
  floorNumber?: string;
  storeImageUrl?: string;
  socialMediaLinks?: Record<string, string>;
  categoryIds?: number[];
}

export class StoreManagementApi {
  constructor(
    private readonly request: APIRequestContext,
    private readonly sessionId: string,
  ) {}

  private get headers() {
    return { 'X-Session-Id': this.sessionId };
  }

  async create(body: CreateStoreBody): Promise<APIResponse> {
    return this.request.post('/api/Stores', {
      data: body,
      headers: this.headers,
    });
  }

  async update(id: string, body: CreateStoreBody): Promise<APIResponse> {
    return this.request.put(`/api/Stores/${id}`, {
      data: body,
      headers: this.headers,
    });
  }

  async getManagedStores(): Promise<APIResponse> {
    return this.request.get('/api/Stores/manage', {
      headers: this.headers,
    });
  }
}
