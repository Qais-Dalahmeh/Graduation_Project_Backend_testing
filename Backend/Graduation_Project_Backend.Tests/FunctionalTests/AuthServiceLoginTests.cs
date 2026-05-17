using Graduation_Project_Backend.DTOs.Auth;
using Graduation_Project_Backend.Models.Entities;
using Graduation_Project_Backend.Models.User;
using Graduation_Project_Backend.Service.Auth;
using Graduation_Project_Backend.Service.Common;
using Graduation_Project_Backend.Service.Session;
using Graduation_Project_Backend.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;

namespace Graduation_Project_Backend.Tests.FunctionalTests
{
    public sealed class AuthServiceLoginTests
    {
        private static AuthService CreateService(AppDbContext db)
            => new AuthService(
                db,
                new PhoneNumberService(),
                new PasswordHasher<UserProfile>(),
                new SessionService(db));

        [Fact]
        public async Task LoginAsync_ValidCredentials_ReturnsSessionWithCorrectUser()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();

            var auth = CreateService(db);
            var registered = await auth.RegisterAsync(new RegisterRequestDto
            {
                Name = "Test User",
                PhoneNumber = "0791234001",
                Password = "MyPass123",
                MallID = mallId
            });

            var login = await auth.LoginAsync(new LoginRequestDto
            {
                PhoneNumber = "0791234001",
                Password = "MyPass123",
                MallID = mallId
            });

            Assert.Equal(registered.UserId, login.UserId);
            Assert.Equal("user", login.Role);
            Assert.False(string.IsNullOrWhiteSpace(login.SessionId));
        }

        [Fact]
        public async Task LoginAsync_WrongPassword_ThrowsUnauthorized()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();

            var auth = CreateService(db);
            await auth.RegisterAsync(new RegisterRequestDto
            {
                Name = "User",
                PhoneNumber = "0791234002",
                Password = "CorrectPass",
                MallID = mallId
            });

            await Assert.ThrowsAsync<AuthUnauthorizedException>(() =>
                auth.LoginAsync(new LoginRequestDto
                {
                    PhoneNumber = "0791234002",
                    Password = "WrongPass",
                    MallID = mallId
                }));
        }

        [Fact]
        public async Task LoginAsync_NonExistentPhone_ThrowsUnauthorized()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<AuthUnauthorizedException>(() =>
                CreateService(db).LoginAsync(new LoginRequestDto
                {
                    PhoneNumber = "0799999999",
                    Password = "SomePass",
                    MallID = mallId
                }));
        }

        [Fact]
        public async Task LoginAsync_CorrectPhoneWrongMall_ThrowsUnauthorized()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallA = Guid.NewGuid();
            Guid mallB = Guid.NewGuid();
            db.Malls.AddRange(
                new Mall { Id = mallA, Name = "Mall A", CreatedAt = DateTimeOffset.UtcNow },
                new Mall { Id = mallB, Name = "Mall B", CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();

            var auth = CreateService(db);
            await auth.RegisterAsync(new RegisterRequestDto
            {
                Name = "User",
                PhoneNumber = "0791234003",
                Password = "Pass123",
                MallID = mallA
            });

            // Same phone, but login against the wrong mall
            await Assert.ThrowsAsync<AuthUnauthorizedException>(() =>
                auth.LoginAsync(new LoginRequestDto
                {
                    PhoneNumber = "0791234003",
                    Password = "Pass123",
                    MallID = mallB
                }));
        }

        [Fact]
        public async Task RegisterAsync_DuplicatePhone_SameMall_ThrowsConflict()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();

            var auth = CreateService(db);
            await auth.RegisterAsync(new RegisterRequestDto
            {
                Name = "First",
                PhoneNumber = "0791234004",
                Password = "Pass123",
                MallID = mallId
            });

            await Assert.ThrowsAsync<AuthConflictException>(() =>
                auth.RegisterAsync(new RegisterRequestDto
                {
                    Name = "Second",
                    PhoneNumber = "0791234004",
                    Password = "Pass456",
                    MallID = mallId
                }));
        }

        [Fact]
        public async Task RegisterAsync_SamePhone_DifferentMall_ThrowsConflict()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallA = Guid.NewGuid();
            Guid mallB = Guid.NewGuid();
            db.Malls.AddRange(
                new Mall { Id = mallA, Name = "Mall A", CreatedAt = DateTimeOffset.UtcNow },
                new Mall { Id = mallB, Name = "Mall B", CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();

            var auth = CreateService(db);
            await auth.RegisterAsync(new RegisterRequestDto
            {
                Name = "User A",
                PhoneNumber = "0791234005",
                Password = "Pass123",
                MallID = mallA
            });

            // Same phone number across different malls is blocked
            await Assert.ThrowsAsync<AuthConflictException>(() =>
                auth.RegisterAsync(new RegisterRequestDto
                {
                    Name = "User B",
                    PhoneNumber = "0791234005",
                    Password = "Pass456",
                    MallID = mallB
                }));
        }

        [Fact]
        public async Task RegisterAsync_EmptyName_ThrowsValidation()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<AuthValidationException>(() =>
                CreateService(db).RegisterAsync(new RegisterRequestDto
                {
                    Name = "   ",
                    PhoneNumber = "0791234006",
                    Password = "Pass123",
                    MallID = mallId
                }));
        }

        [Fact]
        public async Task RegisterAsync_EmptyPassword_ThrowsValidation()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<AuthValidationException>(() =>
                CreateService(db).RegisterAsync(new RegisterRequestDto
                {
                    Name = "User",
                    PhoneNumber = "0791234007",
                    Password = "",
                    MallID = mallId
                }));
        }

        [Fact]
        public async Task LogoutAsync_ValidSession_DeletesSession()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();

            var auth = CreateService(db);
            var reg = await auth.RegisterAsync(new RegisterRequestDto
            {
                Name = "User",
                PhoneNumber = "0791234008",
                Password = "Pass123",
                MallID = mallId
            });

            Assert.Single(db.UserSessions);

            await auth.LogoutAsync(reg.SessionId);

            Assert.Empty(db.UserSessions);
        }

        [Fact]
        public async Task LogoutAsync_NonExistentSession_ThrowsNotFound()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();

            await Assert.ThrowsAsync<AuthNotFoundException>(() =>
                CreateService(db).LogoutAsync("session-that-does-not-exist"));
        }

        [Fact]
        public async Task LoginAsync_CreatesNewSession_ReplacingPreviousOne()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();

            var auth = CreateService(db);
            await auth.RegisterAsync(new RegisterRequestDto
            {
                Name = "User",
                PhoneNumber = "0791234009",
                Password = "Pass123",
                MallID = mallId
            });

            var login1 = await auth.LoginAsync(new LoginRequestDto { PhoneNumber = "0791234009", Password = "Pass123", MallID = mallId });
            var login2 = await auth.LoginAsync(new LoginRequestDto { PhoneNumber = "0791234009", Password = "Pass123", MallID = mallId });

            // Session is replaced â€” only one active session at a time
            Assert.Single(db.UserSessions);
            Assert.NotEqual(login1.SessionId, login2.SessionId);
        }
    }
}

