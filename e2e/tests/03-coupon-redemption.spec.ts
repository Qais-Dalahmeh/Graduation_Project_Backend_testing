import { test, expect } from '../fixtures/base-test';

test.describe('Coupon Discovery & Redemption', () => {
  test('authenticated user can list available coupons', async ({ userApis }) => {
    const res = await userApis.coupons.list();
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(Array.isArray(body)).toBe(true);
  });

  test('listing coupons without session returns 4xx', async ({ request }) => {
    const { CouponsApi } = await import('../api/coupons.api');
    const couponsNoAuth = new CouponsApi(request);
    const res = await couponsNoAuth.list();
    expect(res.status()).toBeGreaterThanOrEqual(400);
  });

  test('user coupon wallet is accessible', async ({ userApis }) => {
    const res = await userApis.coupons.getUserCoupons();
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(Array.isArray(body)).toBe(true);
  });

  test('can read individual coupon details when coupon exists', async ({
    userApis,
  }) => {
    const listRes = await userApis.coupons.list();
    const coupons: { id: string; isActive: boolean }[] = await listRes.json();

    const activeCoupon = coupons.find((c) => c.isActive);
    if (!activeCoupon) {
      test.skip(true, 'No active coupons in DB — skipping detail check');
      return;
    }

    const detailRes = await userApis.coupons.getById(activeCoupon.id);
    expect(detailRes.status()).toBe(200);

    const coupon = await detailRes.json();
    expect(coupon).toHaveProperty('id', activeCoupon.id);
    expect(coupon).toHaveProperty('isActive', true);
  });

  test('user can redeem an available coupon and receives a serial number', async ({
    userApis,
  }) => {
    const listRes = await userApis.coupons.list();
    const coupons: Array<{ id: string; costPoint?: number; isActive: boolean }> =
      await listRes.json();

    const freeCoupon = coupons.find(
      (c) => c.isActive && (!c.costPoint || c.costPoint === 0),
    );

    if (!freeCoupon) {
      test.skip(true, 'No free coupons available — skipping redemption test');
      return;
    }

    const redeemRes = await userApis.coupons.redeem({ couponId: freeCoupon.id });
    expect(redeemRes.status()).toBe(200);

    const result = await redeemRes.json();
    expect(typeof result.serialNumber).toBe('string');
    expect(result.serialNumber).toHaveLength(8);
  });

  test('merchant can validate (redeem-by-serial) a user coupon serial', async ({
    userApis,
  }) => {
    const listRes = await userApis.coupons.list();
    const coupons: Array<{ id: string; costPoint?: number; isActive: boolean }> =
      await listRes.json();

    const freeCoupon = coupons.find(
      (c) => c.isActive && (!c.costPoint || c.costPoint === 0),
    );

    if (!freeCoupon) {
      test.skip(true, 'No free coupons available — skipping serial redemption test');
      return;
    }

    const redeemRes = await userApis.coupons.redeem({ couponId: freeCoupon.id });
    expect(redeemRes.ok()).toBe(true);
    const { serialNumber } = await redeemRes.json();

    const merchantRes = await userApis.coupons.redeemBySerial({ serialNumber });
    expect(merchantRes.status()).toBe(200);
  });

  test('a serial number cannot be consumed twice', async ({ userApis }) => {
    const listRes = await userApis.coupons.list();
    const coupons: Array<{ id: string; costPoint?: number; isActive: boolean }> =
      await listRes.json();

    const freeCoupon = coupons.find(
      (c) => c.isActive && (!c.costPoint || c.costPoint === 0),
    );

    if (!freeCoupon) {
      test.skip(true, 'No free coupons — skipping double-redeem test');
      return;
    }

    const redeemRes = await userApis.coupons.redeem({ couponId: freeCoupon.id });
    if (!redeemRes.ok()) {
      test.skip(true, 'Could not redeem coupon — skipping double-redeem test');
      return;
    }
    const { serialNumber } = await redeemRes.json();

    const firstScan = await userApis.coupons.redeemBySerial({ serialNumber });
    expect(firstScan.ok()).toBe(true);

    const secondScan = await userApis.coupons.redeemBySerial({ serialNumber });
    expect(secondScan.status()).toBeGreaterThanOrEqual(400);
  });
});
