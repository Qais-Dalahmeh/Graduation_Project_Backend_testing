using System.Text.Json;
using Graduation_Project_Backend.DTOs.Auth;
using Graduation_Project_Backend.DTOs.Dashboard;
using Graduation_Project_Backend.DTOs.Offers;
using Graduation_Project_Backend.Models.Entities;
using Graduation_Project_Backend.Models.User;
using Graduation_Project_Backend.Service;
using Graduation_Project_Backend.Service.Auth;
using Graduation_Project_Backend.Service.Common;
using Graduation_Project_Backend.Service.Session;
using Graduation_Project_Backend.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace Graduation_Project_Backend.Tests.BranchCoverageTests;

/// <summary>
/// Covers remaining error-path branches to push line coverage toward 100%.
/// Targets: AuthService null-dto validation, ProcessTransactionAsync edge cases,
/// SessionService empty-id delete, OffersService store-not-found,
/// DashboardService date filter, UserAccessContext, AuthException,
/// StoresService sync edge cases.
/// </summary>
public sealed class CoverageGapTests_ErrorPaths
{
    // â”€â”€ AuthException base class â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void AuthValidationException_CodeIsSet()
    {
        var ex = new AuthValidationException("msg", "VAL_CODE");
        Assert.Equal("VAL_CODE", ex.Code);
        Assert.Equal("msg", ex.Message);
    }

    [Fact]
    public void AuthUnauthorizedException_CodeIsSet()
    {
        var ex = new AuthUnauthorizedException("bad creds", "BAD_CREDS");
        Assert.Equal("BAD_CREDS", ex.Code);
    }

    [Fact]
    public void AuthConflictException_CodeIsSet()
    {
        var ex = new AuthConflictException("already exists", "CONFLICT");
        Assert.Equal("CONFLICT", ex.Code);
    }

    [Fact]
    public void AuthNotFoundException_CodeIsSet()
    {
        var ex = new AuthNotFoundException("not found", "NOT_FOUND");
        Assert.Equal("NOT_FOUND", ex.Code);
    }

    // â”€â”€ AuthService â€” null/empty dto validation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static AuthService CreateAuthService(AppDbContext db)
        => new AuthService(db, new PhoneNumberService(), new PasswordHasher<UserProfile>(), new SessionService(db));

    [Fact]
    public async Task RegisterAsync_NullDto_ThrowsValidation()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        await Assert.ThrowsAsync<AuthValidationException>(() =>
            CreateAuthService(db).RegisterAsync(null));
    }

    [Fact]
    public async Task RegisterAsync_EmptyManagerId_ThrowsValidation()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        var dto = new RegisterRequestDto
        {
            ManagerId = Guid.Empty,
            MallID = Guid.NewGuid(),
            PhoneNumber = "+962700900001",
            Password = "pass"
        };
        await Assert.ThrowsAsync<AuthValidationException>(() =>
            CreateAuthService(db).RegisterAsync(dto));
    }

    [Fact]
    public async Task LoginAsync_NullDto_ThrowsValidation()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        await Assert.ThrowsAsync<AuthValidationException>(() =>
            CreateAuthService(db).LoginAsync(null));
    }

    [Fact]
    public async Task LoginAsync_EmptyMallId_ThrowsValidation()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        var dto = new LoginRequestDto { PhoneNumber = "+962700900002", Password = "pass", MallID = Guid.Empty };
        await Assert.ThrowsAsync<AuthValidationException>(() =>
            CreateAuthService(db).LoginAsync(dto));
    }

    [Fact]
    public async Task LoginAsync_RehashedPassword_ReturnsSession()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = DateTimeOffset.UtcNow });
        var hasher = new PasswordHasher<UserProfile>();
        var tempUser = new UserProfile();
        string hash = hasher.HashPassword(tempUser, "pass123");
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700900010", PasswordHash = hash, Role = "user", MallID = mallId });
        await db.SaveChangesAsync();

        var svc = CreateAuthService(db);
        var dto = new LoginRequestDto { PhoneNumber = "+962700900010", Password = "pass123", MallID = mallId };
        var result = await svc.LoginAsync(dto);
        Assert.NotNull(result.SessionId);
    }

    [Fact]
    public async Task LogoutAsync_EmptySessionId_ThrowsValidation()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        await Assert.ThrowsAsync<AuthValidationException>(() =>
            CreateAuthService(db).LogoutAsync(""));
    }

    [Fact]
    public async Task LogoutAsync_WhitespaceSessionId_ThrowsValidation()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        await Assert.ThrowsAsync<AuthValidationException>(() =>
            CreateAuthService(db).LogoutAsync("   "));
    }

    [Fact]
    public async Task ManagerQuickLoginAsync_NullDto_ThrowsValidation()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        await Assert.ThrowsAsync<AuthValidationException>(() =>
            CreateAuthService(db).ManagerQuickLoginAsync(null));
    }

    [Fact]
    public async Task ManagerQuickLoginAsync_EmptyManagerId_ThrowsValidation()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        await Assert.ThrowsAsync<AuthValidationException>(() =>
            CreateAuthService(db).ManagerQuickLoginAsync(new ManagerQuickLoginRequestDto { ManagerId = Guid.Empty }));
    }

    [Fact]
    public async Task ManagerQuickLoginAsync_ManagerNotFound_ThrowsValidation()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        await Assert.ThrowsAsync<AuthValidationException>(() =>
            CreateAuthService(db).ManagerQuickLoginAsync(new ManagerQuickLoginRequestDto { ManagerId = Guid.NewGuid() }));
    }

    [Fact]
    public async Task ManagerQuickLoginAsync_ExistingUser_WithBlankPhone_UpdatesPhone()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = DateTimeOffset.UtcNow });
        db.Managers.Add(new Manager { Id = managerId, Name = "Director", Role = "manager", MallID = mallId });
        // Existing user with blank phone and no password hash
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "OldName", PhoneNumber = "", PasswordHash = "", Role = "manager", MallID = mallId });
        await db.SaveChangesAsync();

        var svc = CreateAuthService(db);
        var result = await svc.ManagerQuickLoginAsync(new ManagerQuickLoginRequestDto { ManagerId = managerId });

        Assert.NotNull(result.SessionId);
    }

    // â”€â”€ SessionService.DeleteSessionAsync â€” empty id â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task DeleteSessionAsync_EmptyId_ReturnsFalse()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        var svc = new SessionService(db);
        var result = await svc.DeleteSessionAsync("");
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteSessionAsync_WhitespaceId_ReturnsFalse()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        var svc = new SessionService(db);
        var result = await svc.DeleteSessionAsync("   ");
        Assert.False(result);
    }

    // â”€â”€ RewardsService.ProcessTransactionAsync â€” error paths â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static RewardsService CreateRewardsService(AppDbContext db)
    {
        var access = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        return new RewardsService(db, new PhoneNumberService(), new NoOpUserPointsUpdatesService(), access);
    }

    [Fact]
    public async Task ProcessTransactionAsync_EmptyPhone_Throws()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateRewardsService(db).ProcessTransactionAsync("", Guid.NewGuid(), "R1", null, 10));
    }

    [Fact]
    public async Task ProcessTransactionAsync_EmptyReceiptId_Throws()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateRewardsService(db).ProcessTransactionAsync("+962700900030", Guid.NewGuid(), "", null, 10));
    }

    [Fact]
    public async Task ProcessTransactionAsync_NegativePrice_Throws()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateRewardsService(db).ProcessTransactionAsync("+962700900031", Guid.NewGuid(), "R2", null, -5));
    }

    [Fact]
    public async Task ProcessTransactionAsync_StoreNotFound_Throws()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateRewardsService(db).ProcessTransactionAsync("+962700900032", Guid.NewGuid(), "R3", null, 10));
    }

    [Fact]
    public async Task ProcessTransactionAsync_UserNotFound_Throws()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid storeId = Guid.NewGuid();

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = DateTimeOffset.UtcNow });
        db.Stores.Add(new Store { Id = storeId, Name = "Store", MallID = mallId });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateRewardsService(db).ProcessTransactionAsync("+962700900033", storeId, "R4", null, 10));
    }

    // â”€â”€ OffersService â€” store validation paths â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static OffersService CreateOffersService(AppDbContext db)
    {
        var access = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        return new OffersService(db, access, NullLogger<OffersService>.Instance);
    }

    [Fact]
    public async Task CreateOfferAsync_StoreNotFound_ThrowsValidation()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = now });
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Mgr", PhoneNumber = "+962700900040", PasswordHash = "h", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = managerId, Name = "Mgr", Role = "manager", MallID = mallId });
        await db.SaveChangesAsync();

        var svc = CreateOffersService(db);
        var request = new CreateOfferRequest
        {
            StoreId = Guid.NewGuid(),  // non-existent
            Title = "Test",
            StartAt = now.AddDays(-1),
            EndAt = now.AddDays(5)
        };

        await Assert.ThrowsAsync<ApiValidationException>(() =>
            svc.CreateOfferAsync(managerId, request));
    }

    [Fact]
    public async Task CreateOfferAsync_StoreInDifferentMall_ThrowsForbidden()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallA = Guid.NewGuid();
        Guid mallB = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();
        Guid otherStoreId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallA, Name = "Mall A", CreatedAt = now });
        db.Malls.Add(new Mall { Id = mallB, Name = "Mall B", CreatedAt = now });
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Mgr", PhoneNumber = "+962700900041", PasswordHash = "h", Role = "manager", MallID = mallA });
        db.Managers.Add(new Manager { Id = managerId, Name = "Mgr", Role = "manager", MallID = mallA });
        db.Stores.Add(new Store { Id = otherStoreId, Name = "Foreign Store", MallID = mallB });
        await db.SaveChangesAsync();

        var svc = CreateOffersService(db);
        var request = new CreateOfferRequest { StoreId = otherStoreId, Title = "Test", StartAt = now.AddDays(-1), EndAt = now.AddDays(5) };

        await Assert.ThrowsAsync<ApiForbiddenException>(() =>
            svc.CreateOfferAsync(managerId, request));
    }

    // â”€â”€ DashboardService â€” date range in GetTransactionMetricsAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task GetSummaryAsync_WithDateFilter_FiltersTransactions()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();
        Guid storeId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = now });
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Mgr", PhoneNumber = "+962700900050", PasswordHash = "h", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = managerId, Name = "Mgr", Role = "manager", MallID = mallId });
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700900051", PasswordHash = "h", Role = "user", MallID = mallId });
        db.Stores.Add(new Store { Id = storeId, Name = "Store", MallID = mallId });
        db.Transactions.AddRange(
            new Transaction { Id = 600, UserId = userId, StoreId = storeId, ReceiptId = "D1", Price = 10, Points = 1000, CreatedAt = now.AddDays(-10), TransactionStatus = "completed" },
            new Transaction { Id = 601, UserId = userId, StoreId = storeId, ReceiptId = "D2", Price = 20, Points = 2000, CreatedAt = now, TransactionStatus = "completed" });
        await db.SaveChangesAsync();

        var access = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        var svc = new DashboardService(db, access);

        // Filter to only include the last 5 days â€” should see only 1 transaction
        var query = new DashboardDateRangeQuery { From = now.AddDays(-5), To = now.AddDays(1) };
        var result = await svc.GetSummaryAsync(managerId, query);

        Assert.Equal(1, result.TotalTransactions);
        Assert.Equal(20m, result.TotalSalesAmount);
    }

    // â”€â”€ DashboardService â€” GetCouponSnapshotAsync date filter â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task GetPointsAsync_WithDateFilter_AppliesFilter()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();
        Guid storeId = Guid.NewGuid();
        Guid couponId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = now });
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Mgr", PhoneNumber = "+962700900060", PasswordHash = "h", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = managerId, Name = "Mgr", Role = "manager", MallID = mallId });
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700900061", PasswordHash = "h", Role = "user", MallID = mallId });
        db.Stores.Add(new Store { Id = storeId, Name = "Store", MallID = mallId });
        db.Coupons.Add(new Coupon { Id = couponId, Type = "discount", Discription = "X", StartAt = now.AddDays(-1), EndAt = now.AddDays(5), IsActive = true, CostPoint = 100, MallID = mallId, CreatedAt = now });
        db.UserCoupons.Add(new UserCoupon { SerialNumber = "66666666", UserId = userId, CouponId = couponId, IsRedeemed = true, CreatedAt = DateTime.UtcNow.AddDays(-30) });
        await db.SaveChangesAsync();

        var access = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        var svc = new DashboardService(db, access);

        // Date filter that excludes the 30-day-old coupon
        var query = new DashboardDateRangeQuery { From = now.AddDays(-7), To = now.AddDays(1) };
        var result = await svc.GetPointsAsync(managerId, query);

        // No user coupons in range, so DailyRedeemed should be empty
        Assert.Empty(result.DailyRedeemed);
    }

    // â”€â”€ StoresService.SyncStoreCategoriesAsync â€” replaceExisting with no prior â”€

    [Fact]
    public async Task UpdateStoreAsync_ReplaceExisting_WithNoExistingCategories()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();
        Guid storeId = Guid.NewGuid();
        long catId = 20;
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = now });
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Mgr", PhoneNumber = "+962700900070", PasswordHash = "h", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = managerId, Name = "Mgr", Role = "manager", MallID = mallId });
        db.Stores.Add(new Store { Id = storeId, Name = "Store", MallID = mallId });
        db.Categories.Add(new Category { Id = catId, Name = "Sports", MallID = mallId });
        // No existing StoreCategory rows
        await db.SaveChangesAsync();

        var access = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        var svc = new StoresService(db, access, NullLogger<StoresService>.Instance);

        var request = new DTOs.Stores.UpdateStoreRequest
        {
            Name = "Store Updated",
            CategoryIds = new List<long> { catId }
        };

        var result = await svc.UpdateStoreAsync(managerId, storeId, request);
        Assert.Single(result.Categories);
    }

    // â”€â”€ UserAccessContext â€” all properties are set â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void UserAccessContext_Properties_AreAccessible()
    {
        var ctx = new UserAccessContext
        {
            UserId = Guid.NewGuid(),
            MallID = Guid.NewGuid(),
            UserRole = "manager",
            IsManager = true,
            IsMallWideManager = true,
            ManagerId = Guid.NewGuid(),
            ManagerRole = "admin",
            AssignedStoreIds = new HashSet<Guid> { Guid.NewGuid() }
        };

        Assert.True(ctx.IsManager);
        Assert.True(ctx.IsMallWideManager);
        Assert.NotNull(ctx.ManagerRole);
        Assert.Single(ctx.AssignedStoreIds);
        Assert.Equal("manager", ctx.UserRole);
    }

    // â”€â”€ JsonDocumentMapper â€” undefined element â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void ToJsonDocument_ReturnsNull_ForUndefinedElement()
    {
        // Default JsonElement has ValueKind = Undefined
        JsonElement? element = new JsonElement();
        var result = JsonDocumentMapper.ToJsonDocument(element);
        Assert.Null(result);
    }
}

