import { test, expect } from '../fixtures/base-test';
import { AuthApi } from '../api/auth.api';
import { TransactionsApi } from '../api/transactions.api';
import { makeTestUser, uniqueReceiptId, ENV } from '../fixtures/test-data';

test.describe('Receipt Submission & Points Accrual', () => {
  test('submitting a receipt credits points to the user', async ({
    request,
    userSession,
    userApis,
  }) => {
    const pointsBefore = (await userApis.userInfo.getPointsOrFail()).totalPoints;

    const txApi = new TransactionsApi(request);
    const txResult = await txApi.addTransactionOrFail({
      phoneNumber: userSession.phoneNumber,
      storeId: ENV.STORE_ID,
      receiptId: uniqueReceiptId(),
      price: 50.0,
    });

    expect(txResult.transactionId).toBeGreaterThan(0);

    const pointsAfter = (await userApis.userInfo.getPointsOrFail()).totalPoints;
    expect(pointsAfter).toBeGreaterThan(pointsBefore);
  });

  test('user can list their own receipts', async ({
    request,
    userSession,
    userApis,
  }) => {
    const txApi = new TransactionsApi(request);
    await txApi.addTransactionOrFail({
      phoneNumber: userSession.phoneNumber,
      storeId: ENV.STORE_ID,
      receiptId: uniqueReceiptId(),
      price: 30.0,
    });

    const res = await userApis.transactions.getMyReceipts();
    expect(res.status()).toBe(200);

    const body = await res.json();
    expect(body).toHaveProperty('items');
    expect(Array.isArray(body.items)).toBe(true);
    expect(body.items.length).toBeGreaterThanOrEqual(1);
  });

  test('user can read their own receipt details', async ({
    request,
    userSession,
    userApis,
  }) => {
    const txApi = new TransactionsApi(request);
    const txResult = await txApi.addTransactionOrFail({
      phoneNumber: userSession.phoneNumber,
      storeId: ENV.STORE_ID,
      receiptId: uniqueReceiptId(),
      price: 75.0,
    });

    const res = await userApis.transactions.getTransaction(txResult.transactionId);
    expect(res.status()).toBe(200);

    const body = await res.json();
    expect(body).toHaveProperty('transactionId', txResult.transactionId);
    expect(body).toHaveProperty('price', 75);
  });

  test('rejects duplicate receipt ID', async ({ request, userSession }) => {
    const txApi = new TransactionsApi(request);
    const receiptId = uniqueReceiptId();

    const first = await txApi.addTransaction({
      phoneNumber: userSession.phoneNumber,
      storeId: ENV.STORE_ID,
      receiptId,
      price: 20.0,
    });
    expect(first.ok()).toBe(true);

    const duplicate = await txApi.addTransaction({
      phoneNumber: userSession.phoneNumber,
      storeId: ENV.STORE_ID,
      receiptId,
      price: 20.0,
    });
    expect(duplicate.status()).toBeGreaterThanOrEqual(400);
  });

  test('user B cannot access user A receipt details', async ({
    request,
    userSession,
  }) => {
    const txApi = new TransactionsApi(request);
    const txResult = await txApi.addTransactionOrFail({
      phoneNumber: userSession.phoneNumber,
      storeId: ENV.STORE_ID,
      receiptId: uniqueReceiptId(),
      price: 40.0,
    });

    const authApi = new AuthApi(request);
    const userB = makeTestUser({ name: 'User B' });
    const sessionB = await authApi.registerAndLogin(userB);

    const userBTx = new TransactionsApi(request, sessionB.sessionId);
    const res = await userBTx.getTransaction(txResult.transactionId);
    expect(res.status()).toBeGreaterThanOrEqual(400);

    await authApi.logout(sessionB.sessionId);
  });

  test('multiple receipts accumulate points cumulatively', async ({
    request,
    userSession,
    userApis,
  }) => {
    const txApi = new TransactionsApi(request);
    const pointsBefore = (await userApis.userInfo.getPointsOrFail()).totalPoints;

    await txApi.addTransactionOrFail({
      phoneNumber: userSession.phoneNumber,
      storeId: ENV.STORE_ID,
      receiptId: uniqueReceiptId(),
      price: 100.0,
    });
    await txApi.addTransactionOrFail({
      phoneNumber: userSession.phoneNumber,
      storeId: ENV.STORE_ID,
      receiptId: uniqueReceiptId(),
      price: 200.0,
    });

    const pointsAfter = (await userApis.userInfo.getPointsOrFail()).totalPoints;
    expect(pointsAfter).toBeGreaterThan(pointsBefore);
  });
});
