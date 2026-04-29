import { defineConfig } from '@playwright/test';
import * as dotenv from 'dotenv';
import * as path from 'path';

// Load .env.test if present, then fall back to .env
dotenv.config({ path: path.resolve(__dirname, '.env.test') });
dotenv.config({ path: path.resolve(__dirname, '.env') });

export default defineConfig({
  testDir: './tests',
  globalSetup: './global-setup.ts',
  timeout: 30_000,

  // Sequential workers: avoids DB race conditions between tests
  workers: 1,
  fullyParallel: false,

  // Retry flaky tests in CI only
  retries: process.env.CI ? 2 : 0,

  reporter: [
    ['list'],
    ['html', { outputFolder: 'playwright-report', open: 'never' }],
    ['junit', { outputFile: 'test-results/results.xml' }],
  ],

  use: {
    baseURL: process.env.BASE_URL ?? 'http://localhost:5000',
    extraHTTPHeaders: {
      'Content-Type': 'application/json',
      Accept: 'application/json',
    },
    // Auto-wait & smart retries built into Playwright
    actionTimeout: 10_000,
    navigationTimeout: 15_000,
  },
});
