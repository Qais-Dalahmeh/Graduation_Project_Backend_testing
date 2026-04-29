# E2E Behavioral Test Plan — Mall Loyalty System Backend

---

## 1. Objectives

| # | Objective |
|---|-----------|
| 1 | Verify that the full user authentication lifecycle (register → login → session use → logout) works correctly from the client's perspective |
| 2 | Confirm that receipt submission credits loyalty points to the correct user |
| 3 | Validate the complete coupon flow: discovery → redemption by user → merchant serial validation |
| 4 | Ensure the manager role can create, update, publish, and delete offers without privilege leakage |
| 5 | Confirm store browsing is accessible to authenticated users with consistent, scoped data |

---

## 2. Success Criteria

A test run is considered **passing** when:

- All 30+ test cases execute without assertion failures
- No test depends on another test's state (each test is self-contained)
- CI pipeline completes in under 10 minutes on GitHub Actions
- Zero flaky tests across 3 consecutive CI runs
- HTML report is generated and archived as a CI artifact

---

## 3. Tool Selection: Playwright

### Why Playwright?

| Criterion | Decision |
|-----------|----------|
| **API testing** | Playwright has a first-class `APIRequestContext` — no browser needed for REST |
| **TypeScript** | Full native support with strict types |
| **Auto-wait** | Built-in retry logic reduces flaky tests vs raw `fetch` |
| **Fixtures** | Powerful test fixture system enables clean setup/teardown |
| **CI support** | Zero-config GitHub Actions integration |
| **Reporting** | Built-in HTML + JUnit reporters |
| **Parallelism** | Workers can be set to 1 for sequential API tests (avoids DB races) |

### When Playwright is NOT the right choice

- If the team wants to test browser UI interactions only → Cypress has a better DX
- If Mobile (Android/iOS) is the primary target → use Appium
- If only unit-level contract testing is needed → use a lightweight HTTP client like `supertest`

---

## 4. Test Scope

### In Scope

| Module | Scenarios Covered |
|--------|-------------------|
| **Auth** | Registration, login, logout, session expiry, duplicate phone, wrong password |
| **Transactions** | Receipt submission, points accrual, receipt listing, access control |
| **Coupons** | Listing, detail, redeem by ID, merchant serial redemption, double-redeem prevention |
| **Manager** | Quick login, offer CRUD, status toggle, role enforcement |
| **Stores** | List, detail, 404 on missing, unauthenticated blocking, idempotency |

### Out of Scope (not covered by E2E)

| Item | Reason |
|------|--------|
| Dashboard analytics | Requires seeded time-series data; covered by unit tests |
| Real-time SSE stream | Requires WebSocket/EventSource; separate integration test needed |
| Chatbot NLP quality | Non-deterministic; covered by unit tests |
| File upload (receipt images) | Requires multipart form; lower priority for MVP |
| Notifications | No client-facing endpoint; internal system |

---

## 5. Test Architecture

```
e2e/
├── playwright.config.ts        # Runner config (timeout, retries, reporters)
├── package.json
├── tsconfig.json
├── .env.example                # Template for required environment variables
│
├── api/                        # API Object Model (AOM) — replaces Page Objects
│   ├── types.ts                # Shared DTO interfaces matching backend
│   ├── auth.api.ts             # /api/auth endpoints
│   ├── transactions.api.ts     # /api/transactions endpoints
│   ├── coupons.api.ts          # /api/coupons endpoints
│   ├── stores.api.ts           # /api/stores + /api/offers endpoints
│   └── userinfo.api.ts         # /api/userinfo endpoints
│
├── fixtures/
│   ├── test-data.ts            # Data factories (unique phone, receipt ID, etc.)
│   └── base-test.ts            # Extended test fixture with pre-wired API clients
│
└── tests/
    ├── 01-user-registration-login.spec.ts
    ├── 02-transactions-points.spec.ts
    ├── 03-coupon-redemption.spec.ts
    ├── 04-manager-workflow.spec.ts
    └── 05-stores-browsing.spec.ts
```

### API Object Model Pattern

The **API Object Model (AOM)** applies the Page Object Model principle to REST APIs:

- Each controller has a corresponding `*.api.ts` class
- Classes encapsulate `request.get/post/put/delete` calls
- Tests import the class, not raw `request` — details are hidden
- Adding a new endpoint = one method in one file, no test rewrites

---

## 6. Test Data Strategy

### Test Isolation

Every test that creates a user generates a **cryptographically random phone number** via `uniquePhone()`. This ensures:

- No test pollutes another test's user data
- Tests can run in any order (no ordering dependency)
- Failed tests don't leave partially-created state that breaks the next run

### Receipt Deduplication

Receipt IDs are generated with `uniqueReceiptId()` (random hex prefix) to satisfy the unique DB index.

### Manager & Store Seeds

The tests assume:
- `TEST_MALL_ID` — a mall exists in the DB
- `TEST_MANAGER_ID` — a manager exists and can quick-login
- `TEST_STORE_ID` — a store exists for transaction submission

These are **read-only fixtures** (not created or deleted by tests) — set them via `.env.test` or CI secrets.

### Data Reset Between Tests

- **Created users**: cleaned up via logout (sessions expire; no DB delete needed)
- **Created offers**: explicitly deleted in the test's cleanup step
- **Transactions**: append-only by design — not deleted (mirrors production)

---

## 7. Environment Configuration

### Local Development

```bash
# 1. Copy env template
cp e2e/.env.example e2e/.env.test

# 2. Fill in your values
#    BASE_URL=http://localhost:5000
#    TEST_MALL_ID=1
#    TEST_MANAGER_ID=1
#    TEST_STORE_ID=1

# 3. Start the backend (in a separate terminal)
dotnet run --project Graduation_Project_Backend.csproj --urls http://localhost:5000

# 4. Run tests
cd e2e
npm install
npx playwright install --with-deps chromium
npm test
```

### CI (GitHub Actions)

Set these as **Repository Secrets** in GitHub → Settings → Secrets:

| Secret | Description |
|--------|-------------|
| `TEST_DB_CONNECTION_STRING` | PostgreSQL connection string for the test DB |
| `TEST_MALL_ID` | Mall ID seed |
| `TEST_MANAGER_ID` | Manager ID seed |
| `TEST_STORE_ID` | Store ID seed |

The CI workflow (`.github/workflows/e2e.yml`) automatically:
1. Builds the .NET backend
2. Starts it on `localhost:5000`
3. Waits until the server responds on `/swagger/index.html`
4. Runs all Playwright tests
5. Uploads HTML report and JUnit XML as artifacts

---

## 8. Running Tests — CLI Reference

```bash
# All tests (headless, sequential)
npm test

# Single spec file
npx playwright test tests/01-user-registration-login.spec.ts

# Single test by title substring
npx playwright test -g "submitting a receipt credits points"

# Show HTML report after run
npm run test:report

# Debug mode (step through with Playwright Inspector)
npm run test:debug

# CI mode (list + JUnit reporters)
npm run test:ci
```

---

## 9. Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| **Flaky tests from network latency** | Medium | Playwright auto-wait + 2 retries in CI |
| **DB seed IDs missing** | High | CI secrets validated; tests skip gracefully with `test.skip()` |
| **Manager ID not in DB** | High | Document seed requirement; add a seed migration for CI |
| **Points calculation changes** | Low | Tests check `pointsAfter > pointsBefore` (relative), not absolute values |
| **Coupon availability** | Medium | Tests use `test.skip()` when no coupons exist — not a hard failure |
| **Sequential workers = slower** | Low | Workers=1 is intentional for DB integrity; full suite runs in ~3 minutes |
| **Real DB side effects** | Medium | Use a dedicated test schema or wipe test users after CI run |

---

## 10. Scenarios Summary Table

| # | Spec File | Scenario | Tests |
|---|-----------|----------|-------|
| 1 | `01-user-registration-login.spec.ts` | Register → Login → Session → Logout | 6 |
| 2 | `02-transactions-points.spec.ts` | Receipt Submission → Points Accrual | 6 |
| 3 | `03-coupon-redemption.spec.ts` | Coupon Discovery → Redeem → Serial Validation | 7 |
| 4 | `04-manager-workflow.spec.ts` | Manager CRUD on Offers + Role Enforcement | 7 |
| 5 | `05-stores-browsing.spec.ts` | Store Listing → Detail → Access Control | 7 |
| | | **Total** | **33 tests** |
