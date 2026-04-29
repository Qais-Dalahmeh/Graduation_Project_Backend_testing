import { test, expect } from '../fixtures/base-test';
import { futureDate } from '../fixtures/test-data';
import { randomBytes } from 'crypto';

function makeAnnouncement(overrides?: object) {
  return {
    title: `E2E Announcement ${randomBytes(3).toString('hex')}`,
    content: 'Created by automated E2E test — safe to delete.',
    announcementType: 'general',
    priority: 'normal',
    isActive: true,
    isPinned: false,
    startDate: new Date().toISOString(),
    endDate: futureDate(7),
    ...overrides,
  };
}

test.describe('Announcement Lifecycle', () => {
  test('listing announcements without session returns 4xx', async ({ request }) => {
    const { AnnouncementsApi } = await import('../api/announcements.api');
    const noAuth = new AnnouncementsApi(request);
    const res = await noAuth.list();
    expect(res.status()).toBeGreaterThanOrEqual(400);
  });

  test('authenticated user can list announcements', async ({ userApis }) => {
    const res = await userApis.announcements.list();
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(Array.isArray(body)).toBe(true);
  });

  test('manager can create an announcement', async ({ managerApis }) => {
    const payload = makeAnnouncement();
    const res = await managerApis.announcements.create(payload);
    expect(res.ok()).toBe(true);

    const created = await res.json();
    expect(created).toHaveProperty('id');
    expect(created).toHaveProperty('title', payload.title);
    expect(created).toHaveProperty('isActive', true);

    await managerApis.announcements.delete(created.id);
  });

  test('announcement created by manager is visible in public list', async ({
    managerApis,
    userApis,
  }) => {
    const payload = makeAnnouncement();
    const createRes = await managerApis.announcements.create(payload);
    expect(createRes.ok()).toBe(true);
    const created = await createRes.json();

    const listRes = await userApis.announcements.list();
    const list: Array<{ id: string }> = await listRes.json();
    expect(list.some((a) => a.id === created.id)).toBe(true);

    await managerApis.announcements.delete(created.id);
  });

  test('manager can pin and unpin an announcement', async ({ managerApis }) => {
    const createRes = await managerApis.announcements.create(makeAnnouncement());
    const created = await createRes.json();
    expect(created.isPinned).toBe(false);

    const pinRes = await managerApis.announcements.setPin(created.id, true);
    expect(pinRes.ok()).toBe(true);
    const pinned = await pinRes.json();
    expect(pinned.isPinned).toBe(true);

    const unpinRes = await managerApis.announcements.setPin(created.id, false);
    expect(unpinRes.ok()).toBe(true);
    const unpinned = await unpinRes.json();
    expect(unpinned.isPinned).toBe(false);

    await managerApis.announcements.delete(created.id);
  });

  test('manager can deactivate and reactivate an announcement', async ({
    managerApis,
  }) => {
    const createRes = await managerApis.announcements.create(makeAnnouncement());
    const created = await createRes.json();

    const deactivateRes = await managerApis.announcements.setStatus(created.id, false);
    expect(deactivateRes.ok()).toBe(true);
    const deactivated = await deactivateRes.json();
    expect(deactivated.isActive).toBe(false);

    const reactivateRes = await managerApis.announcements.setStatus(created.id, true);
    expect(reactivateRes.ok()).toBe(true);
    const reactivated = await reactivateRes.json();
    expect(reactivated.isActive).toBe(true);

    await managerApis.announcements.delete(created.id);
  });

  test('manager can update announcement title and content', async ({
    managerApis,
  }) => {
    const createRes = await managerApis.announcements.create(makeAnnouncement());
    const created = await createRes.json();

    const newTitle = `Updated ${randomBytes(3).toString('hex')}`;
    const updateRes = await managerApis.announcements.update(created.id, {
      title: newTitle,
      content: 'Updated content by E2E test.',
      isActive: true,
    });
    expect(updateRes.ok()).toBe(true);
    const updated = await updateRes.json();
    expect(updated.title).toBe(newTitle);

    await managerApis.announcements.delete(created.id);
  });

  test('deleted announcement no longer appears in public list', async ({
    managerApis,
    userApis,
  }) => {
    const createRes = await managerApis.announcements.create(makeAnnouncement());
    const created = await createRes.json();

    const deleteRes = await managerApis.announcements.delete(created.id);
    expect(deleteRes.ok()).toBe(true);

    const listRes = await userApis.announcements.list();
    const list: Array<{ id: string }> = await listRes.json();
    expect(list.some((a) => a.id === created.id)).toBe(false);
  });

  test('manager can list their own managed announcements', async ({
    managerApis,
  }) => {
    const createRes = await managerApis.announcements.create(makeAnnouncement());
    const created = await createRes.json();

    const managedRes = await managerApis.announcements.getManagedAnnouncements();
    expect(managedRes.status()).toBe(200);
    const managed: Array<{ id: string }> = await managedRes.json();
    expect(managed.some((a) => a.id === created.id)).toBe(true);

    await managerApis.announcements.delete(created.id);
  });

  test('regular user is forbidden from creating an announcement', async ({
    userApis,
  }) => {
    const res = await userApis.announcements.create(makeAnnouncement());
    expect(res.status()).toBeGreaterThanOrEqual(400);
  });
});
