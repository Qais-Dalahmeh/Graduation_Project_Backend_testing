import { APIRequestContext, APIResponse } from '@playwright/test';
import type { UserPointsDto } from './types';

export class UserInfoApi {
  constructor(
    private readonly request: APIRequestContext,
    private readonly sessionId: string,
  ) {}

  private get headers() {
    return { 'X-Session-Id': this.sessionId };
  }

  async getPoints(): Promise<APIResponse> {
    return this.request.get('/api/userinfo/points', {
      headers: this.headers,
    });
  }

  async getPointsOrFail(): Promise<UserPointsDto> {
    const res = await this.getPoints();
    if (!res.ok()) {
      const text = await res.text();
      throw new Error(`Get points failed (${res.status()}): ${text}`);
    }
    return res.json() as Promise<UserPointsDto>;
  }
}
