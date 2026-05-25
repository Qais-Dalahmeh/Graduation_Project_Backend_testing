import 'package:flutter_test/flutter_test.dart';
import 'package:g_project/Services/auth_service.dart';
import 'package:g_project/Services/api_service.dart';
import 'package:g_project/Services/offers_service.dart';
import 'package:g_project/core/session_store.dart';

/// End-to-End integration tests: Full user journey across Flutter → Backend → DB.
///
/// Flow:
///   1. Register new user
///   2. Login
///   3. Fetch stores
///   4. Fetch offers
///   5. Fetch announcements
///   6. Fetch coupons
///   7. Check receipts (empty for new user)
void main() {
  final authService = AuthService();

  String uniquePhone() {
    final ticks = DateTime.now().millisecondsSinceEpoch.toString();
    return "+96279${ticks.substring(ticks.length - 7)}";
  }

  group('End-to-End: Full User Journey', () {
    test('E2E_RegisterThenLogin_ThenFetchAllData_Succeeds', () async {
      // ── Step 1: Register ────────────────────────────────────────────
      final phone = uniquePhone();
      const password = 'TestPass1!';

      final registerSession = await authService.register(
        name: 'E2E Test User',
        phoneNumber: phone,
        password: password,
      );

      expect(registerSession.sessionId, isNotNull);
      expect(registerSession.sessionId!.isNotEmpty, isTrue);

      // ── Step 2: Login ───────────────────────────────────────────────
      final loginSession = await authService.login(
        phoneNumber: phone,
        password: password,
      );

      expect(loginSession.sessionId, isNotNull);
      final sid = loginSession.sessionId!;

      // Load session into store (required by ApiService)
      SessionStore.current = loginSession;

      // ── Step 3: Fetch Stores ────────────────────────────────────────
      final stores = await ApiService.getStores();
      expect(stores, isA<List>());

      // ── Step 4: Fetch Offers ────────────────────────────────────────
      final offers = await OffersService.getOffers(sid);
      expect(offers, isA<List>());

      // ── Step 5: Fetch Announcements ─────────────────────────────────
      final announcements = await ApiService.getAnnouncements();
      expect(announcements, isA<List>());

      // ── Step 6: Fetch Coupons ───────────────────────────────────────
      final coupons = await ApiService.getCoupons();
      expect(coupons, isA<List>());

      // ── Step 7: Fetch My Receipts (new user → empty) ────────────────
      final receipts = await ApiService.getMyReceipts();
      expect(receipts, isA<Map>());

      // Cleanup
      SessionStore.current = null;
    });

    test('E2E_Register_InitialPoints_AreZero', () async {
      final session = await authService.register(
        name: 'Points Check User',
        phoneNumber: uniquePhone(),
        password: 'TestPass1!',
      );

      expect(session.totalPoints, equals(0));
    });

    test('E2E_TwoUsers_HaveIndependentSessions', () async {
      final phone1 = uniquePhone();
      await Future.delayed(const Duration(milliseconds: 10));
      final phone2 = uniquePhone();

      final session1 = await authService.register(
        name: 'User One',
        phoneNumber: phone1,
        password: 'TestPass1!',
      );
      final session2 = await authService.register(
        name: 'User Two',
        phoneNumber: phone2,
        password: 'TestPass1!',
      );

      // Session IDs must be different
      expect(session1.sessionId, isNot(equals(session2.sessionId)));
      expect(session1.phoneNumber, isNot(equals(session2.phoneNumber)));
    });

    test('E2E_Register_SessionIdFormat_IsValidHexOrGuid', () async {
      final session = await authService.register(
        name: 'SessionId Test User',
        phoneNumber: uniquePhone(),
        password: 'TestPass1!',
      );

      final sid = session.sessionId ?? '';
      // Backend returns a hex token (e.g. SHA-256) or a GUID
      final hexRegex = RegExp(r'^[0-9a-fA-F]{32,}$');
      final guidRegex = RegExp(
        r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$',
      );
      expect(
        hexRegex.hasMatch(sid) || guidRegex.hasMatch(sid),
        isTrue,
        reason: 'SessionId "$sid" is not a valid hex token or GUID',
      );
    });

    test('E2E_BackendIsReachable_BaseUrlResponds', () async {
      // If the backend is down, register will fail —
      // this test is a canary for backend availability.
      expect(
        () => authService.register(
          name: 'Canary User',
          phoneNumber: uniquePhone(),
          password: 'TestPass1!',
        ),
        returnsNormally,
      );
    });
  });
}
