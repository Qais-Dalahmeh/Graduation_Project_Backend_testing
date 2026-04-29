import { APIRequestContext, APIResponse } from '@playwright/test';

export interface DateRangeQuery {
  from?: string;
  to?: string;
}

export class DashboardApi {
  constructor(
    private readonly request: APIRequestContext,
    private readonly sessionId: string,
  ) {}

  private get headers() {
    return { 'X-Session-Id': this.sessionId };
  }

  private buildQs(range?: DateRangeQuery): string {
    const p = new URLSearchParams();
    if (range?.from) p.set('From', range.from);
    if (range?.to) p.set('To', range.to);
    const s = p.toString();
    return s ? `?${s}` : '';
  }

  async getSummary(range?: DateRangeQuery): Promise<APIResponse> {
    return this.request.get(`/api/Dashboard/summary${this.buildQs(range)}`, {
      headers: this.headers,
    });
  }

  async getSales(range?: DateRangeQuery): Promise<APIResponse> {
    return this.request.get(`/api/Dashboard/sales${this.buildQs(range)}`, {
      headers: this.headers,
    });
  }

  async getPoints(range?: DateRangeQuery): Promise<APIResponse> {
    return this.request.get(`/api/Dashboard/points${this.buildQs(range)}`, {
      headers: this.headers,
    });
  }

  async getCoupons(range?: DateRangeQuery): Promise<APIResponse> {
    return this.request.get(`/api/Dashboard/coupons${this.buildQs(range)}`, {
      headers: this.headers,
    });
  }

  async getActivity(range?: DateRangeQuery): Promise<APIResponse> {
    return this.request.get(`/api/Dashboard/activity${this.buildQs(range)}`, {
      headers: this.headers,
    });
  }
}
