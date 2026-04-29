import { test as base } from '@playwright/test';
import { AuthApi } from '../api/auth.api';
import { TransactionsApi } from '../api/transactions.api';
import { CouponsApi } from '../api/coupons.api';
import { StoresApi, OffersApi } from '../api/stores.api';
import { UserInfoApi } from '../api/userinfo.api';
import { AnnouncementsApi } from '../api/announcements.api';
import { DashboardApi } from '../api/dashboard.api';
import { ChatbotApi } from '../api/chatbot.api';
import { StoreManagementApi } from '../api/store-management.api';
import { makeTestUser, ENV } from './test-data';
import type { AuthResponseDto } from '../api/types';

export type ApiFixtures = {
  authApi: AuthApi;
  chatbotApi: ChatbotApi;
  userSession: AuthResponseDto;
  userApis: {
    transactions: TransactionsApi;
    coupons: CouponsApi;
    stores: StoresApi;
    offers: OffersApi;
    userInfo: UserInfoApi;
    announcements: AnnouncementsApi;
  };
  managerSession: AuthResponseDto;
  managerApis: {
    offers: OffersApi;
    stores: StoresApi;
    storeManagement: StoreManagementApi;
    coupons: CouponsApi;
    announcements: AnnouncementsApi;
    dashboard: DashboardApi;
  };
};

export const test = base.extend<ApiFixtures>({
  authApi: async ({ request }, use) => {
    await use(new AuthApi(request));
  },

  chatbotApi: async ({ request }, use) => {
    await use(new ChatbotApi(request));
  },

  userSession: async ({ request }, use) => {
    const authApi = new AuthApi(request);
    const session = await authApi.registerAndLogin(makeTestUser());
    await use(session);
    await authApi.logout(session.sessionId).catch(() => {});
  },

  userApis: async ({ request, userSession }, use) => {
    const sid = userSession.sessionId;
    await use({
      transactions: new TransactionsApi(request, sid),
      coupons: new CouponsApi(request, sid),
      stores: new StoresApi(request, sid),
      offers: new OffersApi(request, sid),
      userInfo: new UserInfoApi(request, sid),
      announcements: new AnnouncementsApi(request, sid),
    });
  },

  managerSession: async ({ request }, use) => {
    const authApi = new AuthApi(request);
    const res = await authApi.managerQuickLogin(ENV.MANAGER_ID);
    if (!res.ok()) {
      throw new Error(
        `Manager quick login failed (${res.status()}): ${await res.text()}`,
      );
    }
    const session: AuthResponseDto = await res.json();
    await use(session);
    await authApi.logout(session.sessionId).catch(() => {});
  },

  managerApis: async ({ request, managerSession }, use) => {
    const sid = managerSession.sessionId;
    await use({
      offers: new OffersApi(request, sid),
      stores: new StoresApi(request, sid),
      storeManagement: new StoreManagementApi(request, sid),
      coupons: new CouponsApi(request, sid),
      announcements: new AnnouncementsApi(request, sid),
      dashboard: new DashboardApi(request, sid),
    });
  },
});

export { expect } from '@playwright/test';
