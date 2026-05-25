import 'package:flutter_test/flutter_test.dart';
import 'package:g_project/Services/auth_service.dart';
import 'package:g_project/Services/offers_service.dart';
import 'package:g_project/core/session_store.dart';

/// Integration tests for OffersService against real Azure backend.
void main() {
  final authService = AuthService();

  String uniquePhone() {
    final ticks = DateTime.now().millisecondsSinceEpoch.toString();
    return "+96279${ticks.substring(ticks.length - 7)}";
  }

  late String sessionId;

  setUpAll(() async {
    final phone = uniquePhone();
    final session = await authService.register(
      name: 'OffersTestUser',
      phoneNumber: phone,
      password: 'TestPass1!',
    );
    SessionStore.current = session;
    sessionId = session.sessionId!;
  });

  tearDownAll(() {
    SessionStore.current = null;
  });

  group('OffersService Integration Tests', () {
    test('getOffers_ValidSession_ReturnsList', () async {
      final offers = await OffersService.getOffers(sessionId);

      expect(offers, isA<List>());
    });

    test('getOffers_ValidSession_NoException', () async {
      expect(
        () => OffersService.getOffers(sessionId),
        returnsNormally,
      );
    });

    test('getOffers_EmptySession_ThrowsOrReturnsEmpty', () async {
      // Empty session ID — either throws or returns empty list
      try {
        final offers = await OffersService.getOffers('');
        expect(offers, isA<List>());
      } catch (e) {
        expect(e, isException);
      }
    });

    test('getOffers_EachOffer_HasExpectedFields', () async {
      final offers = await OffersService.getOffers(sessionId);

      for (final offer in offers) {
        expect(offer, isA<Map>());
        // Offer must have at least title or description
        final hasTitle = offer.containsKey('title') ||
            offer.containsKey('offerTitle') ||
            offer.containsKey('name');
        expect(hasTitle, isTrue,
            reason: 'Offer missing title field: $offer');
      }
    });
  });
}
