# Mall Loyalty System — Testing Suite

Complete testing suite for the Mall Loyalty backend system.  
Covers Unit, Integration, Non-Functional, and API tests.

---

## Repository Structure

```
├── Backend/                  # ASP.NET Core Backend + All C# Tests
│   ├── Graduation_Project_Backend.Tests/          # Unit & Branch Coverage Tests
│   ├── Graduation_Project_Backend.NonFunctionalTests/ # Performance & Reliability Tests
│   └── Graduation_Project_Backend.IntegrationTests/   # Real PostgreSQL via Docker
│
├── Flutter/                  # Flutter Frontend Integration Tests
│   └── test/integration/     # 27 tests against real Azure backend
│
├── nonFunctional/            # Postman Collection (58 API requests)
│   ├── MallLoyalty_Postman_Collection.json
│   └── MallLoyalty_Environment.json
│
└── CoverageReport/           # HTML Coverage Report (98.7% line coverage)
    └── index.html
```

---

## 1 — Backend Tests (C# / xUnit)

### Requirements
- .NET 8 SDK
- Docker Desktop (for Integration Tests only)

### Run Unit Tests
```bash
cd Backend
dotnet test Graduation_Project_Backend.Tests
```

### Run Non-Functional Tests
```bash
cd Backend
dotnet test Graduation_Project_Backend.NonFunctionalTests
```

### Run Integration Tests (requires Docker running)
```bash
cd Backend
dotnet test Graduation_Project_Backend.IntegrationTests
```

### Run All Tests + Coverage
```bash
cd Backend
dotnet test Graduation_Project_Backend.Tests \
  --collect:"XPlat Code Coverage" \
  --results-directory TestResults/Coverage
```

---

## 2 — Coverage Report

Open in browser:
```
CoverageReport/index.html
```

Results:
- **Line Coverage: 98.7%**
- **Branch Coverage: 89.1%**
- **Method Coverage: 96.5%**

---

## 3 — Postman API Tests (58 requests)

### Import into Postman
1. Open Postman
2. Import → `nonFunctional/MallLoyalty_Postman_Collection.json`
3. Import → `nonFunctional/MallLoyalty_Environment.json`
4. Select environment **"Mall Loyalty - Local"** (top right)
5. Click **Run Collection**

### Folders covered
| Folder | Description |
|--------|-------------|
| Auth | Register, Login, Logout |
| Stores | List, Details |
| Offers | CRUD + Active offers |
| Coupons | List, Redeem, User coupons |
| Transactions | Add receipt, History |
| Dashboard | Summary, Sales, Points, Activity |
| Validation Tests | Invalid inputs → 400/404/409 |

---

## 4 — Flutter Integration Tests (27 tests)

Tests the Flutter frontend services against the real Azure backend.

### Requirements
- Flutter SDK 3.x (`flutter --version`)

### Run
```bash
cd Flutter
flutter pub get
flutter test test/integration/
```

### Test files
| File | Tests | What it tests |
|------|-------|---------------|
| `auth_integration_test.dart` | 9 | Register, Login, error cases |
| `api_integration_test.dart` | 9 | Stores, Coupons, Announcements, Receipts |
| `offers_integration_test.dart` | 4 | Offers list and validation |
| `end_to_end_integration_test.dart` | 5 | Full user journey end-to-end |

---

## Test Summary

| Project | Count | Tool |
|---------|-------|------|
| `Graduation_Project_Backend.Tests` (Unit + Branch + Service + Functional) | **313** | xUnit + InMemory EF |
| `Graduation_Project_Backend.NonFunctionalTests` (Performance + Reliability) | **64** | xUnit |
| `Graduation_Project_Backend.IntegrationTests` (Real PostgreSQL via Docker) | **43** | Testcontainers + PostgreSQL |
| Flutter Integration Tests (Flutter → Azure Backend → DB) | **27** | flutter test |
| Postman API Tests | **58 requests** | Postman |
| **Total Automated Tests** | **447** | |
