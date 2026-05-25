import 'package:flutter_test/flutter_test.dart';
import 'package:g_project/Services/auth_service.dart';
import 'package:g_project/models/user_session.dart';

/// Integration tests for AuthService against real Azure backend.
/// These tests call the real deployed API at:
/// https://yallarewards-hfhxdxerb8caa8g9.switzerlandnorth-01.azurewebsites.net
void main() {
  final authService = AuthService();

  // Generate a unique phone number for each test run
  String uniquePhone() {
    final ticks = DateTime.now().millisecondsSinceEpoch.toString();
    return "+96279${ticks.substring(ticks.length - 7)}";
  }

  group('AuthService Integration Tests', () {
    // ── Register ─────────────────────────────────────────────────────────

    test('register_ValidData_ReturnsUserSessionWithSessionId', () async {
      final phone = uniquePhone();

      final session = await authService.register(
        name: 'TestUser',
        phoneNumber: phone,
        password: 'TestPass1!',
      );

      expect(session, isA<UserSession>());
      expect(session.sessionId, isNotNull);
      expect(session.sessionId!.isNotEmpty, isTrue);
      expect(session.phoneNumber, equals(phone));
    });

    test('register_DuplicatePhone_ThrowsException', () async {
      final phone = uniquePhone();

      // First registration — must succeed
      await authService.register(
        name: 'TestUser',
        phoneNumber: phone,
        password: 'TestPass1!',
      );

      // Second registration with same phone — must throw
      expect(
        () => authService.register(
          name: 'TestUser2',
          phoneNumber: phone,
          password: 'TestPass1!',
        ),
        throwsException,
      );
    });

    test('register_InvalidPhone_ThrowsException', () async {
      expect(
        () => authService.register(
          name: 'TestUser',
          phoneNumber: 'invalid-phone',
          password: 'TestPass1!',
        ),
        throwsException,
      );
    });

    test('register_EmptyName_ThrowsException', () async {
      expect(
        () => authService.register(
          name: '',
          phoneNumber: uniquePhone(),
          password: 'TestPass1!',
        ),
        throwsException,
      );
    });

    // ── Login ─────────────────────────────────────────────────────────────

    test('login_AfterRegister_ReturnsValidSession', () async {
      final phone = uniquePhone();
      const password = 'TestPass1!';

      // Register first
      await authService.register(
        name: 'LoginTestUser',
        phoneNumber: phone,
        password: password,
      );

      // Then login
      final session = await authService.login(
        phoneNumber: phone,
        password: password,
      );

      expect(session, isA<UserSession>());
      expect(session.sessionId, isNotNull);
      expect(session.sessionId!.isNotEmpty, isTrue);
    });

    test('login_WrongPassword_ThrowsException', () async {
      final phone = uniquePhone();

      await authService.register(
        name: 'WrongPassUser',
        phoneNumber: phone,
        password: 'CorrectPass1!',
      );

      expect(
        () => authService.login(
          phoneNumber: phone,
          password: 'WrongPassword999!',
        ),
        throwsException,
      );
    });

    test('login_UnregisteredPhone_ThrowsException', () async {
      expect(
        () => authService.login(
          phoneNumber: '+96270000000',
          password: 'SomePass1!',
        ),
        throwsException,
      );
    });

    test('register_ReturnsCorrectName', () async {
      final phone = uniquePhone();
      const name = 'Integration Test User';

      final session = await authService.register(
        name: name,
        phoneNumber: phone,
        password: 'TestPass1!',
      );

      // name should be returned in the session
      expect(session.name, equals(name));
    });

    test('register_InitialPointsAreZeroOrPositive', () async {
      final session = await authService.register(
        name: 'PointsUser',
        phoneNumber: uniquePhone(),
        password: 'TestPass1!',
      );

      expect(session.totalPoints, greaterThanOrEqualTo(0));
    });
  });
}
