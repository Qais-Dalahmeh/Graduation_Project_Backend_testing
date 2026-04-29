import { APIRequestContext, APIResponse } from '@playwright/test';

export interface CreateAnnouncementBody {
  title: string;
  content: string;
  storeId?: string;
  announcementType?: string;
  priority?: string;
  isActive?: boolean;
  isPinned?: boolean;
  imageUrl?: string;
  startDate?: string;
  endDate?: string;
}

export class AnnouncementsApi {
  constructor(
    private readonly request: APIRequestContext,
    private readonly sessionId?: string,
  ) {}

  private sessionHeaders() {
    return this.sessionId ? { 'X-Session-Id': this.sessionId } : {};
  }

  async list(): Promise<APIResponse> {
    return this.request.get('/api/Announcements', {
      headers: this.sessionHeaders(),
    });
  }

  async getManagedAnnouncements(): Promise<APIResponse> {
    return this.request.get('/api/Announcements/manage', {
      headers: this.sessionHeaders(),
    });
  }

  async create(body: CreateAnnouncementBody): Promise<APIResponse> {
    return this.request.post('/api/Announcements', {
      data: body,
      headers: this.sessionHeaders(),
    });
  }

  async update(id: string, body: CreateAnnouncementBody): Promise<APIResponse> {
    return this.request.put(`/api/Announcements/${id}`, {
      data: body,
      headers: this.sessionHeaders(),
    });
  }

  async delete(id: string): Promise<APIResponse> {
    return this.request.delete(`/api/Announcements/${id}`, {
      headers: this.sessionHeaders(),
    });
  }

  async setStatus(id: string, isActive: boolean): Promise<APIResponse> {
    return this.request.patch(`/api/Announcements/${id}/status`, {
      data: { isActive },
      headers: this.sessionHeaders(),
    });
  }

  async setPin(id: string, isPinned: boolean): Promise<APIResponse> {
    return this.request.patch(`/api/Announcements/${id}/pin`, {
      data: { isPinned },
      headers: this.sessionHeaders(),
    });
  }
}
