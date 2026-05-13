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

namespace Graduation_Project_Backend.Tests.CoverageGapTests;

/// <summary>
/// Fourth wave of branch-coverage tests — final push to ≥ 90% branch coverage.
///
/// Covers:
///   • AuthService.RegisterManagerAsync — manager already registered, mall mismatch, phone same-mall
///   • AuthService.NormalizePhone       — ArgumentException → AuthValidationException catch branch
///   • DashboardService.ValidateDateRange — invalid date range → throw
///   • DashboardService.GetCouponsAsync  — store-manager path (coupons == null → IsScopeLimited=true)
///   • DashboardService.GetPointsAsync   — store-manager path (coupons?.DailyRedeemed null → ?? [])
/// </summary>
public sealed class BranchCoverageGap4
{
    // ══════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════

    private static AuthService BuildAuth(out Graduation_Project_Backend.Data.AppDbContext db)
    {
        db = TestInfrastructure.CreateDbContext();
        return new AuthService(
            db,
            new PhoneNumberService(),
            new PasswordHasher<UserProfile>(),
            new SessionService(db));
    }

    private static DashboardService BuildDashboard(
        out Graduation_Project_Backend.Data.AppDbContext db,
        out Guid userId,
        out Guid mallId,
        bool isMallWide = true)
    {
        db     = TestInfrastructure.CreateDbContext();
        mallId = Guid.NewGuid();
        userId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile
        {
            Id = userId, Name = "DashMgr", PhoneNumber = "+962799800001",
            PasswordHash = "x", Role = "manager", MallID = mallId
        });
        db.Managers.Add(new Manager { Id = userId, Name = "DashMgr", MallID = mallId, Role = isMallWide ? "manager" : "store_manager" });

        if (!isMallWide)
        {
            var storeId = Guid.NewGuid();
            db.Stores.Add(new Store { Id = storeId, Name = "DS", MallID = mallId });
            db.Management.Add(new Management { ManagerId = userId, StoreId = storeId });
        }

        db.SaveChanges();

        return new DashboardService(
            db,
            new UserAccessService(db, NullLogger<UserAccessService>.Instance));
    }

    // ══════════════════════════════════════════════════════════════════
    // AuthService — RegisterManagerAsync uncovered branches
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Register_ManagerAlreadyRegisteredAsUser_Throws()
    {
        // Covers RegisterManagerAsync:
        //   UserProfile? existingUserByManagerId = await _db.UserProfiles.FirstOrDefaultAsync(u => u.Id == managerId)
        //   if (existingUserByManagerId != null) → TRUE → throw MANAGER_ALREADY_REGISTERED
        var svc = BuildAuth(out var db);
        var mallId    = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        // Manager row exists in Managers table
        db.Managers.Add(new Manager { Id = managerId, Name = "AlreadyMgr", MallID = mallId, Role = "manager" });
        // UserProfile with same ID already exists → manager already registered
        db.UserProfiles.Add(new UserProfile
        {
            Id = managerId, Name = "AlreadyMgr", PhoneNumber = "+962799800002",
            PasswordHash = "x", Role = "manager", MallID = mallId
        });
        await db.SaveChangesAsync();

        var req = new RegisterRequestDto
        {
            Name = "AlreadyMgr", PhoneNumber = "+962799800003",
            Password = "Pass1234!", MallID = mallId, ManagerId = managerId
        };

        await Assert.ThrowsAsync<AuthConflictException>(() => svc.RegisterAsync(req));
    }

    [Fact]
    public async Task Register_ManagerBelongsToDifferentMall_Throws()
    {
        // Covers RegisterManagerAsync:
        //   if (manager.MallID != request.MallID) → TRUE → throw MANAGER_MALL_MISMATCH
        var svc = BuildAuth(out var db);
        var mallA     = Guid.NewGuid();
        var mallB     = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        // Manager belongs to mallA, but request says mallB
        db.Managers.Add(new Manager { Id = managerId, Name = "WrongMallMgr", MallID = mallA, Role = "manager" });
        await db.SaveChangesAsync();

        var req = new RegisterRequestDto
        {
            Name = "WrongMallMgr", PhoneNumber = "+962799800004",
            Password = "Pass1234!", MallID = mallB,   // ← wrong mall
            ManagerId = managerId
        };

        await Assert.ThrowsAsync<AuthValidationException>(() => svc.RegisterAsync(req));
    }

    [Fact]
    public async Task Register_ManagerPhoneAlreadyExistsInSameMall_Throws()
    {
        // Covers RegisterManagerAsync:
        //   if (existingUserByPhone != null) → TRUE
        //     if (existingUserByPhone.MallID == request.MallID) → TRUE → throw USER_ALREADY_EXISTS
        //   (Previous tests only covered the MallID != request.MallID path.)
        var svc = BuildAuth(out var db);
        var mallId    = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        const string phone = "+962799800005";

        db.Managers.Add(new Manager { Id = managerId, Name = "PhoneMgr", MallID = mallId, Role = "manager" });
        // A regular user with the SAME phone and SAME mall already exists
        db.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(), Name = "ExistingUser", PhoneNumber = phone,
            PasswordHash = "x", Role = "user", MallID = mallId
        });
        await db.SaveChangesAsync();

        var req = new RegisterRequestDto
        {
            Name = "PhoneMgr", PhoneNumber = phone,   // same phone as existing user
            Password = "Pass1234!", MallID = mallId,
            ManagerId = managerId
        };

        await Assert.ThrowsAsync<AuthConflictException>(() => svc.RegisterAsync(req));
    }

    [Fact]
    public async Task Register_InvalidPhoneFormat_ThrowsAuthValidation()
    {
        // Covers AuthService.NormalizePhone:
        //   catch (ArgumentException ex) → throw new AuthValidationException(ex.Message, "INVALID_PHONE_NUMBER")
        //   PhoneNumberService.Normalize("not-a-phone") throws ArgumentException → gets wrapped.
        var svc = BuildAuth(out _);

        var req = new RegisterRequestDto
        {
            Name = "Anyone", PhoneNumber = "not-a-valid-phone",
            Password = "Pass1234!", MallID = Guid.NewGuid()
        };

        await Assert.ThrowsAsync<AuthValidationException>(() => svc.RegisterAsync(req));
    }

    // ══════════════════════════════════════════════════════════════════
    // DashboardService — ValidateDateRange throw branch
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetSummary_InvalidDateRange_Throws()
    {
        // Covers DashboardService.ValidateDateRange (sync helper in main class):
        //   if (query.From.HasValue && query.To.HasValue && query.From > query.To) → TRUE → throw
        var svc = BuildDashboard(out _, out var userId, out _, isMallWide: true);

        var query = new DashboardDateRangeQuery
        {
            From = DateTimeOffset.UtcNow.AddDays(5),
            To   = DateTimeOffset.UtcNow.AddDays(1)   // To < From → invalid
        };

        await Assert.ThrowsAsync<ApiValidationException>(() => svc.GetSummaryAsync(userId, query));
    }

    // ══════════════════════════════════════════════════════════════════
    // DashboardService — store-manager path (coupons is null)
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetCoupons_AsStoreManager_ReturnsScopeLimited()
    {
        // Covers GetCouponsAsync:
        //   GetCouponSnapshotAsync returns null for store managers (!access.IsMallWideManager)
        //   if (coupons == null) → TRUE → return new DashboardCouponsResponse { IsScopeLimited = true }
        var svc = BuildDashboard(out _, out var userId, out _, isMallWide: false);

        var result = await svc.GetCouponsAsync(userId, new DashboardDateRangeQuery());

        Assert.True(result.IsScopeLimited);
    }

    [Fact]
    public async Task GetPoints_AsStoreManager_DailyRedeemedIsEmpty()
    {
        // Covers GetPointsAsync:
        //   coupons?.DailyRedeemed.GroupBy(...)...ToList() ?? []
        //   coupons is null for store managers → coupons?.DailyRedeemed = null
        //   → null chain → ?? [] → dailyRedeemed = [] (empty list)
        var svc = BuildDashboard(out _, out var userId, out _, isMallWide: false);

        var result = await svc.GetPointsAsync(userId, new DashboardDateRangeQuery());

        Assert.Empty(result.DailyRedeemed);
        Assert.Null(result.TotalPointsRedeemed);
    }

    // ══════════════════════════════════════════════════════════════════
    // DashboardService — ValidateDateRange: From set, To null (B=false short-circuit)
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetSales_OnlyFromDate_DoesNotThrow()
    {
        // Covers DashboardService.ValidateDateRange line 322:
        //   if (query.From.HasValue && query.To.HasValue && query.From > query.To)
        //   → From.HasValue=true, To.HasValue=false  → short-circuit at B → no throw
        //   This covers the 8th branch (B=false short-circuit) that was previously uncovered.
        var svc = BuildDashboard(out _, out var userId, out _, isMallWide: true);

        var query = new DashboardDateRangeQuery
        {
            From = DateTimeOffset.UtcNow.AddDays(-7),
            To   = null   // only From is set → To.HasValue=false → B short-circuits
        };

        // Should not throw
        var result = await svc.GetSalesAsync(userId, query);
        Assert.NotNull(result);
    }

    // ══════════════════════════════════════════════════════════════════
    // OffersService — NormalizeRequired null branch (line 201)
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateOffer_NullTitle_Throws()
    {
        // Covers OffersService.NormalizeRequired (line 201):
        //   string normalized = NormalizeOptional(value) ?? string.Empty
        //   When Title is null → NormalizeOptional(null) = null → null ?? string.Empty = ""
        //   → IsNullOrWhiteSpace("") = true → throw (covers the TRUE branch of ??)
        var db      = TestInfrastructure.CreateDbContext();
        var mallId  = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile
        {
            Id = userId, Name = "Mgr5", PhoneNumber = "+962799800006",
            PasswordHash = "x", Role = "manager", MallID = mallId
        });
        db.Managers.Add(new Manager { Id = userId, Name = "Mgr5", MallID = mallId, Role = "manager" });
        db.Stores.Add(new Store { Id = storeId, Name = "S5", MallID = mallId });
        await db.SaveChangesAsync();

        var svc = new OffersService(
            db,
            new UserAccessService(db, NullLogger<UserAccessService>.Instance),
            NullLogger<OffersService>.Instance);

        var req = new CreateOfferRequest
        {
            StoreId = storeId, Title = null!,   // null title → NormalizeRequired TRUE branch
            StartAt = DateTimeOffset.UtcNow,
            EndAt   = DateTimeOffset.UtcNow.AddDays(5)
        };

        await Assert.ThrowsAsync<ApiValidationException>(() => svc.CreateOfferAsync(userId, req));
    }

    // ══════════════════════════════════════════════════════════════════
    // RewardsService — RedeemCouponAsync user-not-found branch (line 269)
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RedeemCoupon_UserNotFound_Throws()
    {
        // Covers RedeemCouponAsync line 269:
        //   UserProfile user = await GetUserByIdAsync(userId)
        //       ?? throw new InvalidOperationException("User not found");
        //   → user == null → throw (TRUE branch of ??) — previously uncovered.
        var db       = TestInfrastructure.CreateDbContext();
        var svc      = new RewardsService(db, new PhoneNumberService(), new NoOpUserPointsUpdatesService(), new UserAccessService(db, NullLogger<UserAccessService>.Instance));
        var couponId = Guid.NewGuid();

        db.Coupons.Add(new Coupon
        {
            Id = couponId, Type = "Test", IsActive = true,
            StartAt   = DateTimeOffset.UtcNow.AddDays(-1),
            EndAt     = DateTimeOffset.UtcNow.AddDays(10),
            CostPoint = null,   // free — skips DeductPoints
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var nonExistentUserId = Guid.NewGuid();   // no UserProfile with this ID

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RedeemCouponAsync(nonExistentUserId, couponId));
    }
}
