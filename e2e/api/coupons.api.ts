import { APIRequestContext, APIResponse } from '@playwright/test';
import type { RedeemCouponBySerialDto, RedeemCouponDto } from './types';

export class CouponsApi {
  constructor(
    private readonly request: APIRequestContext,
    private readonly sessionId?: string,
  ) {}

  private sessionHeaders() {
    return this.sessionId ? { 'X-Session-Id': this.sessionId } : {};
  }

  async list(): Promise<APIResponse> {
    return this.request.get('/api/coupons', {
      headers: this.sessionHeaders(),
    });
  }

  async getById(id: string): Promise<APIResponse> {
    return this.request.get(`/api/coupons/${id}`, {
      headers: this.sessionHeaders(),
    });
  }

  async redeem(body: RedeemCouponDto): Promise<APIResponse> {
    return this.request.post('/api/coupons/redeem', {
      data: body,
      headers: this.sessionHeaders(),
    });
  }

  async redeemBySerial(body: RedeemCouponBySerialDto): Promise<APIResponse> {
    return this.request.post('/api/coupons/redeem-by-serial', { data: body });
  }

  async getUserCoupons(): Promise<APIResponse> {
    return this.request.get('/api/coupons/user', {
      headers: this.sessionHeaders(),
    });
  }
}
