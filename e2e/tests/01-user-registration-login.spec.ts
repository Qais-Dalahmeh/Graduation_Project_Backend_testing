import { test, expect } from '../fixtures/base-test';
import { AuthApi } from '../api/auth.api';
import { UserInfoApi } from '../api/userinfo.api';
import { makeTestUser, ENV } from '../fixtures/test-data';

test.describe('User Registration & Login', () => {
  test('registers a new user and receives a session ID', async ({ request }) => {
    const authApi = new AuthApi(request);
    const user = makeTestUser({ name: 'Fatima Al-Test' });

    const res = await authApi.register(user);

    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body).toMatchObject({
      phoneNumber: expect.any(String),
      name: 'Fatima Al-Test',
      role: 'user',
      sessionId: expect.stringMatching(/^[0-9a-f]{64}$/i),
    });

    await authApi.logout(body.sessionId);
  });

  test('returns points = 0 for a brand-new user', async ({ request }) => {
    const authApi = new AuthApi(request);
    const user = makeTestUser();
    const session = await authApi.registerAndLogin(user);

    expect(session.totalPoints).toBe(0);

    await authApi.logout(session.sessionId);
  });

  test('newly registered user can access protected /api/userinfo/points', async ({
    request,
  }) => {
    const authApi = new AuthApi(request);
    const user = makeTestUser();
    const session = await authApi.registerAndLogin(user);

    const userInfo = new UserInfoApi(request, session.sessionId);
    const pointsRes = await userInfo.getPoints();

    expect(pointsRes.status()).toBe(200);
    const body = await pointsRes.json();
    expect(body).toHaveProperty('totalPoints');

    await authApi.logout(session.sessionId);
  });

  test('session is rejected after logout', async ({ request }) => {
    const authApi = new AuthApi(request);
    const user = makeTestUser();
    const session = await authApi.registerAndLogin(user);

    await authApi.logout(session.sessionId);

    const userInfo = new UserInfoApi(request, session.sessionId);
    const res = await userInfo.getPoints();
    expect(res.status()).toBeGreaterThanOrEqual(400);
  });

  test('user can log in again after logout', async ({ request }) => {
    const authApi = new AuthApi(request);
    const user = makeTestUser();

    const reg = await authApi.registerAndLogin(user);
    await authApi.logout(reg.sessionId);

    const login = await authApi.loginOrFail({
      phoneNumber: user.phoneNumber,
      password: user.password,
      mallId: ENV.MALL_ID,
    });

    expect(login.sessionId).toBeDefined();
    expect(login.sessionId).toMatch(/^[0-9a-f]{64}$/i);
    expect(login.sessionId).not.toBe(reg.sessionId);

    await authApi.logout(login.sessionId);
  });

  test('rejects registration with an already-used phone number', async ({
    request,
  }) => {
    const authApi = new AuthApi(request);
    const user = makeTestUser();

    const first = await authApi.register(user);
    expect(first.ok()).toBe(true);
    const firstSession = await first.json();

    const duplicate = await authApi.register(user);
    expect(duplicate.status()).toBeGreaterThanOrEqual(400);

    await authApi.logout(firstSession.sessionId);
  });

  test('rejects login with wrong password', async ({ request }) => {
    const authApi = new AuthApi(request);
    const user = makeTestUser();
    const session = await authApi.registerAndLogin(user);

    const badLogin = await authApi.login({
      phoneNumber: user.phoneNumber,
      password: 'wrongpassword',
      mallId: ENV.MALL_ID,
    });

    expect(badLogin.status()).toBeGreaterThanOrEqual(400);

    await authApi.logout(session.sessionId);
  });

  test('protected endpoint returns 4xx without X-Session-Id header', async ({
    request,
  }) => {
    const userInfo = new UserInfoApi(request, '');
    const res = await userInfo.getPoints();
    expect(res.status()).toBeGreaterThanOrEqual(400);
  });
});
