import { randomBytes } from 'crypto';
import type { RegisterRequestDto } from '../api/types';

// TEST_MALL_ID, TEST_STORE_ID, TEST_MANAGER_ID are populated by global-setup.ts
export const ENV = {
  BASE_URL: process.env.BASE_URL ?? 'http://localhost:5000',
  get MALL_ID(): string {
    const v = process.env.TEST_MALL_ID;
    if (!v) throw new Error('TEST_MALL_ID not set — global-setup may have failed');
    return v;
  },
  get MANAGER_ID(): string {
    const v = process.env.TEST_MANAGER_ID;
    if (!v) throw new Error('TEST_MANAGER_ID not set — global-setup may have failed');
    return v;
  },
  get STORE_ID(): string {
    const v = process.env.TEST_STORE_ID;
    if (!v) throw new Error('TEST_STORE_ID not set — global-setup may have failed');
    return v;
  },
} as const;

export function uniqueReceiptId(): string {
  return `RCPT-${randomBytes(6).toString('hex').toUpperCase()}`;
}

export function uniquePhone(): string {
  const suffix = randomBytes(4)
    .readUInt32BE(0)
    .toString()
    .padStart(8, '0')
    .slice(0, 8);
  return `+9627${suffix}`;
}

export interface TestUser {
  name?: string;
  phoneNumber?: string;
  password?: string;
  mallId?: string;
}

export function makeTestUser(overrides?: TestUser): RegisterRequestDto {
  return {
    name: overrides?.name ?? 'Test User',
    phoneNumber: overrides?.phoneNumber ?? uniquePhone(),
    password: overrides?.password ?? 'Test@1234',
    mallId: overrides?.mallId ?? ENV.MALL_ID,
  };
}

export function futureDate(daysFromNow: number): string {
  const d = new Date();
  d.setDate(d.getDate() + daysFromNow);
  return d.toISOString();
}

export function makeTestOffer(storeId: string) {
  return {
    storeId,
    title: `E2E Test Offer ${randomBytes(3).toString('hex')}`,
    description: 'Created by automated E2E test — safe to delete',
    startAt: new Date().toISOString(),
    endAt: futureDate(7),
    isActive: true,
  };
}
