import { test, expect } from '../fixtures/base-test';
import { makeTestOffer, futureDate, ENV } from '../fixtures/test-data';

test.describe('Manager Workflow — Offer Lifecycle', () => {
  test('manager quick-login returns a manager-role session', async ({
    managerSession,
  }) => {
    expect(managerSession.role).toBe('manager');
    expect(managerSession.sessionId).toMatch(/^[0-9a-f]{64}$/i);
  });

  test('manager can create an offer and it appears in managed list', async ({
    managerApis,
  }) => {
    const offer = makeTestOffer(ENV.STORE_ID);

    const createRes = await managerApis.offers.create(offer);
    expect(createRes.ok()).toBe(true);

    const created = await createRes.json();
    expect(created).toHaveProperty('id');
    expect(created).toHaveProperty('title', offer.title);
    expect(created).toHaveProperty('isActive', true);

    const managedRes = await managerApis.offers.getManagedOffers();
    expect(managedRes.status()).toBe(200);
    const managed: Array<{ id: number }> = await managedRes.json();
    expect(managed.some((o) => o.id === created.id)).toBe(true);

    await managerApis.offers.delete(created.id);
  });

  test('offer created by manager is visible to regular users', async ({
    managerApis,
    userApis,
  }) => {
    const offer = makeTestOffer(ENV.STORE_ID);
    const createRes = await managerApis.offers.create(offer);
    expect(createRes.ok()).toBe(true);
    const created = await createRes.json();

    const listRes = await userApis.offers.list();
    expect(listRes.status()).toBe(200);
    const offers: Array<{ id: number; isActive: boolean }> = await listRes.json();
    const found = offers.find((o) => o.id === created.id);
    expect(found).toBeDefined();
    expect(found?.isActive).toBe(true);

    await managerApis.offers.delete(created.id);
  });

  test('manager can deactivate and re-activate an offer', async ({
    managerApis,
  }) => {
    const createRes = await managerApis.offers.create(makeTestOffer(ENV.STORE_ID));
    const created = await createRes.json();
    expect(created.isActive).toBe(true);

    const deactivateRes = await managerApis.offers.setStatus(created.id, false);
    expect(deactivateRes.ok()).toBe(true);
    const deactivated = await deactivateRes.json();
    expect(deactivated.isActive).toBe(false);

    const reactivateRes = await managerApis.offers.setStatus(created.id, true);
    expect(reactivateRes.ok()).toBe(true);
    const reactivated = await reactivateRes.json();
    expect(reactivated.isActive).toBe(true);

    await managerApis.offers.delete(created.id);
  });

  test('manager can update offer title and description', async ({
    managerApis,
  }) => {
    const createRes = await managerApis.offers.create(makeTestOffer(ENV.STORE_ID));
    const created = await createRes.json();

    const updatedTitle = `Updated Offer ${Date.now()}`;
    const updateRes = await managerApis.offers.update(created.id, {
      storeId: ENV.STORE_ID,
      title: updatedTitle,
      startAt: new Date().toISOString(),
      endAt: futureDate(7),
      isActive: true,
    });
    expect(updateRes.ok()).toBe(true);
    const updated = await updateRes.json();
    expect(updated.title).toBe(updatedTitle);

    await managerApis.offers.delete(created.id);
  });

  test('manager can delete an offer and it disappears from lists', async ({
    managerApis,
    userApis,
  }) => {
    const createRes = await managerApis.offers.create(makeTestOffer(ENV.STORE_ID));
    const created = await createRes.json();

    const deleteRes = await managerApis.offers.delete(created.id);
    expect(deleteRes.ok()).toBe(true);

    const listRes = await userApis.offers.list();
    const offers: Array<{ id: number }> = await listRes.json();
    expect(offers.some((o) => o.id === created.id)).toBe(false);
  });

  test('regular user is forbidden from creating an offer', async ({
    userApis,
  }) => {
    const offer = makeTestOffer(ENV.STORE_ID);
    const res = await userApis.offers.create(offer);
    expect(res.status()).toBeGreaterThanOrEqual(400);
  });
});
