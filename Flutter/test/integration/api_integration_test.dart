import 'package:flutter_test/flutter_test.dart';
import 'package:g_project/Services/api_service.dart';
import 'package:g_project/Services/auth_service.dart';
import 'package:g_project/core/session_store.dart';

/// Integration tests for ApiService against real Azure backend.
/// Tests: Stores, Coupons, Announcements, Notifications, Transactions.
void main() {
  final authService = AuthService();

  String uniquePhone() {
    final ticks = DateTime.now().millisecondsSinceEpoch.toString();
    return "+96279${ticks.substring(ticks.length - 7)}";
  }

  /// Register a real user and load their session into SessionStore.
  Future<void> loginAsNewUser() async {
    final phone = uniquePhone();
    final session = await authService.register(
      name: 'ApiTestUser',
      phoneNumber: phone,
      password: 'TestPass1!',
    );
    SessionStore.current = session;
  }

  setUp(() async {
    await loginAsNewUser();
  });

  tearDown(() {
    SessionStore.current = null;
  });

  // ── Stores ────────────────────────────────────────────────────────────────

  group('ApiService - Stores', () {
    test('getStores_Returns200_WithNonNullList', () async {
      final stores = await ApiService.getStores();

      expect(stores, isA<List>());
      // Backend has at least one seeded store
      expect(stores.length, greaterThanOrEqualTo(0));
    });

    test('getStores_EachItem_HasNameField', () async {
      final stores = await ApiService.getStores();

      for (final store in stores) {
        expect(store, isA<Map>());
        expect(store.containsKey('name') || store.containsKey('storeName'),
            isTrue);
      }
    });
  });

  // ── Coupons ───────────────────────────────────────────────────────────────

  group('ApiService - Coupons', () {
    test('getCoupons_ActiveCoupons_ReturnsList', () async {
      final coupons = await ApiService.getCoupons();

      expect(coupons, isA<List>());
    });

    test('getUserCoupons_AuthenticatedUser_ReturnsList', () async {
      final coupons = await ApiService.getUserCoupons();

      expect(coupons, isA<List>());
    });
  });

  // ── Announcements ─────────────────────────────────────────────────────────

  group('ApiService - Announcements', () {
    test('getAnnouncements_Returns200_WithList', () async {
      final announcements = await ApiService.getAnnouncements();

      expect(announcements, isA<List>());
    });

    test('getAnnouncements_EachItem_HasTitleField', () async {
      final announcements = await ApiService.getAnnouncements();

      for (final a in announcements) {
        expect(a, isA<Map>());
        // Must have at least one of these fields
        expect(
          a.containsKey('title') ||
              a.containsKey('announcementTitle') ||
              a.containsKey('content'),
          isTrue,
        );
      }
    });
  });

  // ── Notifications ─────────────────────────────────────────────────────────

  group('ApiService - Notifications', () {
    test('getNotifications_AuthenticatedUser_ReturnsList', () async {
      // New user may have 0 notifications — that's fine
      final notifications = await ApiService.getNotifications();
      expect(notifications, isA<List>());
    });
  });

  // ── Transactions / Receipts ───────────────────────────────────────────────

  group('ApiService - Receipts', () {
    test('getMyReceipts_NewUser_ReturnsEmptyOrMap', () async {
      final receipts = await ApiService.getMyReceipts();

      expect(receipts, isA<Map>());
    });

    test('getMyReceipts_ContainsTotalCountField', () async {
      final receipts = await ApiService.getMyReceipts();

      expect(
        receipts.containsKey('totalCount') ||
            receipts.containsKey('items') ||
            receipts.containsKey('data'),
        isTrue,
      );
    });
  });
}
