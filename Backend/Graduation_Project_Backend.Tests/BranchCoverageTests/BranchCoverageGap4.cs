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
/// Fourth wave of branch-coverage tests â€” final push to â‰¥ 90% branch coverage.
///
/// Covers:
///   â€¢ AuthService.RegisterManagerAsync â€” manager already registered, mall mismatch, phone same-mall
///   â€¢ AuthService.NormalizePhone       â€” ArgumentException â†’ AuthValidationException catch branch
///   â€¢ DashboardService.ValidateDateRange â€” invalid date range â†’ throw
///   â€¢ DashboardService.GetCouponsAsync  â€” store-manager path (coupons == null â†’ IsScopeLimited=true)
///   â€¢ DashboardService.GetPointsAsync   â€” store-manager path (coupons?.DailyRedeemed null â†’ ?? [])
/// </summary>
public sealed class BranchCoverageGap4
{
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // Helpers
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // AuthService â€” RegisterManagerAsync uncovered branches
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public async Task Register_ManagerAlreadyRegisteredAsUser_Throws()
    {
        // Covers RegisterManagerAsync:
        //   UserProfile? existingUserByManagerId = await _db.UserProfiles.FirstOrDefaultAsync(u => u.Id == managerId)
        //   if (existingUserByManagerId != null) â†’ TRUE â†’ throw MANAGER_ALREADY_REGISTERED
        var svc = BuildAuth(out var db);
        var mallId    = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        // Manager row exists in Managers table
        db.Managers.Add(new Manager { Id = managerId, Name = "AlreadyMgr", MallID = mallId, Role = "manager" });
        // UserProfile with same ID already exists â†’ manager already registered
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
        //   if (manager.MallID != request.MallID) â†’ TRUE â†’ throw MANAGER_MALL_MISMATCH
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
            Password = "Pass1234!", MallID = mallB,   // â† wrong mall
            ManagerId = managerId
        };

        await Assert.ThrowsAsync<AuthValidationException>(() => svc.RegisterAsync(req));
    }

    [Fact]
    public async Task Register_ManagerPhoneAlreadyExistsInSameMall_Throws()
    {
        // Covers RegisterManagerAsync:
        //   if (existingUserByPhone != null) â†’ TRUE
        //     if (existingUserByPhone.MallID == request.MallID) â†’ TRUE â†’ throw USER_ALREADY_EXISTS
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
        //   catch (ArgumentException ex) â†’ throw new AuthValidationException(ex.Message, "INVALID_PHONE_NUMBER")
        //   PhoneNumberService.Normalize("not-a-phone") throws ArgumentException â†’ gets wrapped.
        var svc = BuildAuth(out _);

        var req = new RegisterRequestDto
        {
            Name = "Anyone", PhoneNumber = "not-a-valid-phone",
            Password = "Pass1234!", MallID = Guid.NewGuid()
        };

        await Assert.ThrowsAsync<AuthValidationException>(() => svc.RegisterAsync(req));
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // DashboardService â€” ValidateDateRange throw branch
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public async Task GetSummary_InvalidDateRange_Throws()
    {
        // Covers DashboardService.ValidateDateRange (sync helper in main class):
        //   if (query.From.HasValue && query.To.HasValue && query.From > query.To) â†’ TRUE â†’ throw
        var svc = BuildDashboard(out _, out var userId, out _, isMallWide: true);

        var query = new DashboardDateRangeQuery
        {
            From = DateTimeOffset.UtcNow.AddDays(5),
            To   = DateTimeOffset.UtcNow.AddDays(1)   // To < From â†’ invalid
        };

        await Assert.ThrowsAsync<ApiValidationException>(() => svc.GetSummaryAsync(userId, query));
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // DashboardService â€” store-manager path (coupons is null)
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public async Task GetCoupons_AsStoreManager_ReturnsScopeLimited()
    {
        // Covers GetCouponsAsync:
        //   GetCouponSnapshotAsync returns null for store managers (!access.IsMallWideManager)
        //   if (coupons == null) â†’ TRUE â†’ return new DashboardCouponsResponse { IsScopeLimited = true }
        var svc = BuildDashboard(out _, out var userId, out _, isMallWide: false);

        var result = await svc.GetCouponsAsync(userId, new DashboardDateRangeQuery());

        Assert.True(result.IsScopeLimited);
    }

    [Fact]
    public async Task GetPoints_AsStoreManager_DailyRedeemedIsEmpty()
    {
        // Covers GetPointsAsync:
        //   coupons?.DailyRedeemed.GroupBy(...)...ToList() ?? []
        //   coupons is null for store managers â†’ coupons?.DailyRedeemed = null
        //   â†’ null chain â†’ ?? [] â†’ dailyRedeemed = [] (empty list)
        var svc = BuildDashboard(out _, out var userId, out _, isMallWide: false);

        var result = await svc.GetPointsAsync(userId, new DashboardDateRangeQuery());

        Assert.Empty(result.DailyRedeemed);
        Assert.Null(result.TotalPointsRedeemed);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // DashboardService â€” ValidateDateRange: From set, To null (B=false short-circuit)
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public async Task GetSales_OnlyFromDate_DoesNotThrow()
    {
        // Covers DashboardService.ValidateDateRange line 322:
        //   if (query.From.HasValue && query.To.HasValue && query.From > query.To)
        //   â†’ From.HasValue=true, To.HasValue=false  â†’ short-circuit at B â†’ no throw
        //   This covers the 8th branch (B=false short-circuit) that was previously uncovered.
        var svc = BuildDashboard(out _, out var userId, out _, isMallWide: true);

        var query = new DashboardDateRangeQuery
        {
            From = DateTimeOffset.UtcNow.AddDays(-7),
            To   = null   // only From is set â†’ To.HasValue=false â†’ B short-circuits
        };

        // Should not throw
        var result = await svc.GetSalesAsync(userId, query);
        Assert.NotNull(result);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // OffersService â€” NormalizeRequired null branch (line 201)
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public async Task CreateOffer_NullTitle_Throws()
    {
        // Covers OffersService.NormalizeRequired (line 201):
        //   string normalized = NormalizeOptional(value) ?? string.Empty
        //   When Title is null â†’ NormalizeOptional(null) = null â†’ null ?? string.Empty = ""
        //   â†’ IsNullOrWhiteSpace("") = true â†’ throw (covers the TRUE branch of ??)
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
            StoreId = storeId, Title = null!,   // null title â†’ NormalizeRequired TRUE branch
            StartAt = DateTimeOffset.UtcNow,
            EndAt   = DateTimeOffset.UtcNow.AddDays(5)
        };

        await Assert.ThrowsAsync<ApiValidationException>(() => svc.CreateOfferAsync(userId, req));
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // RewardsService â€” RedeemCouponAsync user-not-found branch (line 269)
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public async Task RedeemCoupon_UserNotFound_Throws()
    {
        // Covers RedeemCouponAsync line 269:
        //   UserProfile user = await GetUserByIdAsync(userId)
        //       ?? throw new InvalidOperationException("User not found");
        //   â†’ user == null â†’ throw (TRUE branch of ??) â€” previously uncovered.
        var db       = TestInfrastructure.CreateDbContext();
        var svc      = new RewardsService(db, new PhoneNumberService(), new NoOpUserPointsUpdatesService(), new UserAccessService(db, NullLogger<UserAccessService>.Instance));
        var couponId = Guid.NewGuid();

        db.Coupons.Add(new Coupon
        {
            Id = couponId, Type = "Test", IsActive = true,
            StartAt   = DateTimeOffset.UtcNow.AddDays(-1),
            EndAt     = DateTimeOffset.UtcNow.AddDays(10),
            CostPoint = null,   // free â€” skips DeductPoints
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var nonExistentUserId = Guid.NewGuid();   // no UserProfile with this ID

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RedeemCouponAsync(nonExistentUserId, couponId));
    }
}

