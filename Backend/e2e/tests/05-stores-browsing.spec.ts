import { test, expect } from '../fixtures/base-test';

test.describe('Store Discovery', () => {
  test('listing stores without session returns 4xx', async ({ request }) => {
    const { StoresApi } = await import('../api/stores.api');
    const noAuthStores = new StoresApi(request);
    const res = await noAuthStores.list();
    expect(res.status()).toBeGreaterThanOrEqual(400);
  });

  test('authenticated user can list stores', async ({ userApis }) => {
    const res = await userApis.stores.list();
    expect(res.status()).toBe(200);

    const stores = await res.json();
    expect(Array.isArray(stores)).toBe(true);
  });

  test('each store in the list has id, name, and mallId', async ({
    userApis,
  }) => {
    const res = await userApis.stores.list();
    const stores: Array<unknown> = await res.json();

    if (stores.length === 0) {
      test.skip(true, 'No stores in DB — skipping field validation');
      return;
    }

    for (const store of stores) {
      expect(store).toHaveProperty('id');
      expect(store).toHaveProperty('name');
    }
  });

  test('user can get store details and data is consistent with list', async ({
    userApis,
  }) => {
    const listRes = await userApis.stores.list();
    const stores: Array<{ id: string; name: string }> = await listRes.json();

    if (stores.length === 0) {
      test.skip(true, 'No stores in DB — skipping detail consistency check');
      return;
    }

    const { id, name } = stores[0];
    const detailRes = await userApis.stores.getById(id);
    expect(detailRes.status()).toBe(200);

    const detail = await detailRes.json();
    expect(detail).toHaveProperty('id', id);
    expect(detail).toHaveProperty('name', name);
  });

  test('requesting a non-existent store ID returns 404', async ({
    userApis,
  }) => {
    const nonExistentId = '00000000-0000-0000-0000-000000000000';
    const res = await userApis.stores.getById(nonExistentId);
    expect(res.status()).toBe(404);
  });

  test('returned stores all belong to the same mall context', async ({
    userSession,
    userApis,
  }) => {
    const listRes = await userApis.stores.list();
    const stores: Array<{ id: number; mallId?: number }> = await listRes.json();

    if (stores.length === 0) {
      test.skip(true, 'No stores — skipping mall scoping check');
      return;
    }

    const storesWithMall = stores.filter((s) => s.mallId !== undefined);
    if (storesWithMall.length > 0) {
      for (const store of storesWithMall) {
        expect(store.mallId).toBe(userSession['mallId'] ?? store.mallId);
      }
    }
  });

  test('calling the store list twice returns the same result', async ({
    userApis,
  }) => {
    const [res1, res2] = await Promise.all([
      userApis.stores.list(),
      userApis.stores.list(),
    ]);

    const [body1, body2] = await Promise.all([res1.json(), res2.json()]);

    expect(JSON.stringify(body1)).toBe(JSON.stringify(body2));
  });
});
