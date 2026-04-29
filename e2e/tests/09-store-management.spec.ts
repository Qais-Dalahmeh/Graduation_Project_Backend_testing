import { test, expect } from '../fixtures/base-test';
import { randomBytes } from 'crypto';

function makeStore(overrides?: object) {
  return {
    name: `E2E Store ${randomBytes(3).toString('hex')}`,
    description: 'Created by automated E2E test — safe to delete.',
    floorNumber: '1',
    operatingHours: '9:00 AM - 10:00 PM',
    ...overrides,
  };
}

test.describe('Store Management', () => {
  test('manager can create a new store', async ({ managerApis }) => {
    const payload = makeStore();
    const res = await managerApis.storeManagement.create(payload);
    expect(res.ok()).toBe(true);

    const created = await res.json();
    expect(created).toHaveProperty('id');
    expect(created).toHaveProperty('name', payload.name);
  });

  test('created store appears in the managed stores list', async ({
    managerApis,
  }) => {
    const payload = makeStore();
    const createRes = await managerApis.storeManagement.create(payload);
    expect(createRes.ok()).toBe(true);
    const created = await createRes.json();

    const managedRes = await managerApis.storeManagement.getManagedStores();
    expect(managedRes.status()).toBe(200);
    const managed: Array<{ id: string }> = await managedRes.json();
    expect(managed.some((s) => s.id === created.id)).toBe(true);
  });

  test('created store is visible to regular users in store listing', async ({
    managerApis,
    userApis,
  }) => {
    const payload = makeStore();
    const createRes = await managerApis.storeManagement.create(payload);
    expect(createRes.ok()).toBe(true);
    const created = await createRes.json();

    const listRes = await userApis.stores.list();
    expect(listRes.status()).toBe(200);
    const stores: Array<{ id: string }> = await listRes.json();
    expect(stores.some((s) => s.id === created.id)).toBe(true);
  });

  test('manager can update store name and description', async ({
    managerApis,
  }) => {
    const createRes = await managerApis.storeManagement.create(makeStore());
    const created = await createRes.json();

    const newName = `Updated Store ${randomBytes(3).toString('hex')}`;
    const updateRes = await managerApis.storeManagement.update(created.id, {
      name: newName,
      description: 'Updated by E2E test.',
    });
    expect(updateRes.ok()).toBe(true);
    const updated = await updateRes.json();
    expect(updated.name).toBe(newName);
  });

  test('manager managed stores list is accessible', async ({ managerApis }) => {
    const res = await managerApis.storeManagement.getManagedStores();
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(Array.isArray(body)).toBe(true);
  });

  test('regular user session is forbidden from creating a store', async ({
    request,
    userSession,
  }) => {
    const { StoreManagementApi } = await import('../api/store-management.api');
    const userStoreApi = new StoreManagementApi(request, userSession.sessionId);
    const res = await userStoreApi.create(makeStore());
    expect(res.status()).toBeGreaterThanOrEqual(400);
  });

  test('creating a store without session returns 4xx', async ({ request }) => {
    const { StoreManagementApi } = await import('../api/store-management.api');
    const noAuth = new StoreManagementApi(request, '');
    const res = await noAuth.create(makeStore());
    expect(res.status()).toBeGreaterThanOrEqual(400);
  });
});
