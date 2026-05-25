# Flutter Integration Tests

Integration tests for the Mall Loyalty Flutter frontend.  
Tests run against the real deployed Azure backend.

**Backend URL:** `https://yallarewards-hfhxdxerb8caa8g9.switzerlandnorth-01.azurewebsites.net`

## Requirements

- Flutter SDK 3.x  
  Install: https://flutter.dev/docs/get-started/install

## Setup

```bash
flutter pub get
```

## Run Tests

```bash
# Run all integration tests
flutter test test/integration/

# Run specific file
flutter test test/integration/auth_integration_test.dart
flutter test test/integration/end_to_end_integration_test.dart
```

## Test Files

### auth_integration_test.dart (9 tests)
- Register new user → returns valid session
- Duplicate phone → throws exception
- Invalid phone format → throws exception
- Login after register → returns valid session
- Wrong password → throws exception
- Unregistered phone → throws exception
- Returns correct name after register
- Initial points are zero

### api_integration_test.dart (9 tests)
- Get stores → returns list
- Each store has name field
- Get active coupons → returns list
- Get user coupons → returns list
- Get announcements → returns 200
- Get notifications → returns list
- Get receipts (new user) → returns map
- Receipts contains totalCount field

### offers_integration_test.dart (4 tests)
- Get offers with valid session → returns list
- Get offers → no exception thrown
- Empty session → handled gracefully
- Each offer has title field

### end_to_end_integration_test.dart (5 tests)
- Full journey: Register → Login → Fetch all data
- New user initial points = 0
- Two users have independent sessions
- Session ID is valid hex token
- Backend is reachable
