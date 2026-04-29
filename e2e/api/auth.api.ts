import { APIRequestContext, APIResponse } from '@playwright/test';
import type {
  AuthResponseDto,
  LoginRequestDto,
  RegisterRequestDto,
} from './types';

export class AuthApi {
  constructor(private readonly request: APIRequestContext) {}

  async register(body: RegisterRequestDto): Promise<APIResponse> {
    return this.request.post('/api/auth/register', { data: body });
  }

  async login(body: LoginRequestDto): Promise<APIResponse> {
    return this.request.post('/api/auth/login', { data: body });
  }

  async managerQuickLogin(managerId: number): Promise<APIResponse> {
    return this.request.post('/api/auth/manager-quick-login', {
      data: { managerId },
    });
  }

  async logout(sessionId: string): Promise<APIResponse> {
    return this.request.post('/api/auth/logout', {
      data: { sessionId },
    });
  }

  async registerAndLogin(body: RegisterRequestDto): Promise<AuthResponseDto> {
    const res = await this.register(body);
    if (!res.ok()) {
      const text = await res.text();
      throw new Error(`Registration failed (${res.status()}): ${text}`);
    }
    return res.json() as Promise<AuthResponseDto>;
  }

  async loginOrFail(body: LoginRequestDto): Promise<AuthResponseDto> {
    const res = await this.login(body);
    if (!res.ok()) {
      const text = await res.text();
      throw new Error(`Login failed (${res.status()}): ${text}`);
    }
    return res.json() as Promise<AuthResponseDto>;
  }
}
