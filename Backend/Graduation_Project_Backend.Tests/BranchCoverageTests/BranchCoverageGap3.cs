using Graduation_Project_Backend.DTOs.Announcements;
using Graduation_Project_Backend.DTOs.Dashboard;
using Graduation_Project_Backend.DTOs.Offers;
using Graduation_Project_Backend.Models.Entities;
using Graduation_Project_Backend.Models.User;
using Graduation_Project_Backend.Service;
using Graduation_Project_Backend.Service.Common;
using Graduation_Project_Backend.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Graduation_Project_Backend.Tests.BranchCoverageTests;

/// <summary>
/// Third wave of branch-coverage tests.
/// Targets the remaining uncovered branches to push past 90% branch coverage.
///
/// Specifically covers:
///   â€¢ AnnouncementsService.UpdateAnnouncementAsync â€” both ?? branches for AnnouncementType and Priority
///   â€¢ DashboardService.GetActivityAsync â€” store-manager (non-mall-wide) path skips unreadNotifications
///   â€¢ RewardsService â€” ProcessTransaction store-not-found, user-not-found, null-description
///   â€¢ RewardsService â€” RedeemCoupon not-started-yet, RedeemCouponBySerial serial-not-found
///   â€¢ RewardsService â€” GetUserCoupons with orphaned (null) Coupon navigation covers all ?. null branches
///   â€¢ OffersService â€” store manager creating offer for their own assigned store (success, no throw)
/// </summary>
public sealed class BranchCoverageGap3
{
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // AnnouncementsService â€” UpdateAnnouncementAsync uncovered branches
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    private static (AnnouncementsService svc, Guid userId, Guid mallId, Guid storeId)
        BuildAnnouncementsForUpdate()
    {
        var db     = TestInfrastructure.CreateDbContext();
        var mallId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile
        {
            Id = userId, Name = "Mgr3", PhoneNumber = "+962799900001",
            PasswordHash = "x", Role = "manager", MallID = mallId
        });
        db.Managers.Add(new Manager { Id = userId, Name = "Mgr3", MallID = mallId, Role = "manager" });
        db.Stores.Add(new Store { Id = storeId, Name = "Store3", MallID = mallId });
        db.SaveChanges();

        var svc = new AnnouncementsService(
            db,
            new UserAccessService(db, NullLogger<UserAccessService>.Instance),
            NullLogger<AnnouncementsService>.Instance);

        return (svc, userId, mallId, storeId);
    }

    [Fact]
    public async Task UpdateAnnouncement_ExplicitTypeAndPriority_UpdatesCorrectly()
    {
        // Covers UpdateAnnouncementAsync lines 104-105:
        //   NormalizeOptional("promotion") = "promotion" (non-null) â†’ "promotion" ?? "general" = "promotion" (FALSE branch of ??)
        //   NormalizeOptional("high")      = "high"      (non-null) â†’ "high"      ?? "normal"  = "high"      (FALSE branch of ??)
        var (svc, userId, _, _) = BuildAnnouncementsForUpdate();

        var createReq = new CreateAnnouncementRequest
        {
            Title   = "Original", Content = "Content",
            StartDate = DateTimeOffset.UtcNow, EndDate = DateTimeOffset.UtcNow.AddDays(3)
        };
        var created = await svc.CreateAnnouncementAsync(userId, createReq);

        var updateReq = new UpdateAnnouncementRequest
        {
            Title            = "Updated",
            Content          = "Updated content",
            AnnouncementType = "promotion",
            Priority         = "high",
            StartDate        = DateTimeOffset.UtcNow,
            EndDate          = DateTimeOffset.UtcNow.AddDays(5)
        };
        var result = await svc.UpdateAnnouncementAsync(userId, created.Id, updateReq);

        Assert.Equal("promotion", result.AnnouncementType);
        Assert.Equal("high",      result.Priority);
        Assert.Equal("Updated",   result.Title);
    }

    [Fact]
    public async Task UpdateAnnouncement_EmptyTypeAndPriority_FallsBackToDefaults()
    {
        // Covers UpdateAnnouncementAsync lines 104-105:
        //   NormalizeOptional("") = null â†’ null ?? "general" = "general" (TRUE branch of ??)
        //   NormalizeOptional("") = null â†’ null ?? "normal"  = "normal"  (TRUE branch of ??)
        var (svc, userId, _, _) = BuildAnnouncementsForUpdate();

        var createReq = new CreateAnnouncementRequest
        {
            Title   = "Base", Content = "Base content",
            AnnouncementType = "event", Priority = "high",
            StartDate = DateTimeOffset.UtcNow, EndDate = DateTimeOffset.UtcNow.AddDays(3)
        };
        var created = await svc.CreateAnnouncementAsync(userId, createReq);

        var updateReq = new UpdateAnnouncementRequest
        {
            Title            = "Changed",
            Content          = "Changed content",
            AnnouncementType = "",       // NormalizeOptional("") â†’ null â†’ use default "general"
            Priority         = "   ",   // NormalizeOptional("   ") â†’ null â†’ use default "normal"
            StartDate        = DateTimeOffset.UtcNow,
            EndDate          = DateTimeOffset.UtcNow.AddDays(5)
        };
        var result = await svc.UpdateAnnouncementAsync(userId, created.Id, updateReq);

        Assert.Equal("general", result.AnnouncementType);
        Assert.Equal("normal",  result.Priority);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // DashboardService â€” GetActivityAsync store-manager path
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public async Task GetActivity_AsStoreManager_UnreadNotificationsIsNull()
    {
        // Covers DashboardService line 157:
        //   if (access.IsMallWideManager) â€” FALSE branch
        //   Store managers skip the unreadNotifications query; result.UnreadNotifications == null.
        var db     = TestInfrastructure.CreateDbContext();
        var mallId  = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile
        {
            Id = userId, Name = "SM3", PhoneNumber = "+962799900002",
            PasswordHash = "x", Role = "manager", MallID = mallId
        });
        db.Managers.Add(new Manager { Id = userId, Name = "SM3", MallID = mallId, Role = "store_manager" });
        db.Stores.Add(new Store { Id = storeId, Name = "S3", MallID = mallId });
        db.Management.Add(new Management { ManagerId = userId, StoreId = storeId });
        await db.SaveChangesAsync();

        var svc = new DashboardService(
            db,
            new UserAccessService(db, NullLogger<UserAccessService>.Instance));

        var result = await svc.GetActivityAsync(userId, new DashboardDateRangeQuery());

        Assert.Null(result.UnreadNotifications);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // RewardsService â€” uncovered ProcessTransaction branches
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    private static RewardsService BuildRewards3(out Graduation_Project_Backend.Data.AppDbContext db)
    {
        db = TestInfrastructure.CreateDbContext();
        return new RewardsService(
            db,
            new PhoneNumberService(),
            new NoOpUserPointsUpdatesService(),
            new UserAccessService(db, NullLogger<UserAccessService>.Instance));
    }

    [Fact]
    public async Task ProcessTransaction_StoreNotInDatabase_Throws()
    {
        // Covers ProcessTransactionAsync:
        //   Guid? mallId = await GetMallIdByStoreIdAsync(storeId);
        //   if (mallId == null) â†’ TRUE branch â€” store doesn't exist in DB.
        var svc = BuildRewards3(out _);
        var nonExistentStoreId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ProcessTransactionAsync("+962799900003", nonExistentStoreId, "RX-NOSTORE", "desc", 50m));
    }

    [Fact]
    public async Task ProcessTransaction_UserPhoneNotRegistered_Throws()
    {
        // Covers ProcessTransactionAsync:
        //   UserProfile user = await GetUserByPhoneAndMallIdAsync(â€¦) ?? throw  â†’ null-coalescing throw branch.
        //   The store exists so mallId is valid, but no user has this phone number.
        var svc = BuildRewards3(out var db);
        var mallId  = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        db.Stores.Add(new Store { Id = storeId, Name = "Sx", MallID = mallId });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ProcessTransactionAsync("+962799900004", storeId, "RX-NOUSER", "desc", 50m));
    }

    [Fact]
    public async Task ProcessTransaction_NullDescription_StillSucceeds()
    {
        // Covers CreateTransactionAsync:
        //   ReceiptDescription = description ?? ""  â€” TRUE branch (description is null â†’ uses "").
        var svc = BuildRewards3(out var db);
        var mallId  = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var phone   = "+962799900005";

        db.Stores.Add(new Store { Id = storeId, Name = "Sy", MallID = mallId });
        db.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(), Name = "U3", PhoneNumber = phone,
            PasswordHash = "x", Role = "user", MallID = mallId
        });
        await db.SaveChangesAsync();

        var result = await svc.ProcessTransactionAsync(phone, storeId, "RX-NULLDESC", null, 10m);

        Assert.NotNull(result);
        Assert.Equal(storeId, result.StoreId);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // RewardsService â€” RedeemCoupon "not started yet" branch
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public async Task RedeemCoupon_NotStartedYet_Throws()
    {
        // Covers RedeemCouponAsync:
        //   if (coupon.StartAt > now || coupon.EndAt < now)
        //   â†’ StartAt > now evaluates TRUE (short-circuits) â†’ throw.
        //   Previous tests only covered the EndAt < now path.
        var svc = BuildRewards3(out var db);
        var userId   = Guid.NewGuid();
        var couponId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile
        {
            Id = userId, Name = "U4", PhoneNumber = "+962799900006",
            PasswordHash = "x", Role = "user", MallID = Guid.NewGuid(), TotalPoints = 9999
        });
        db.Coupons.Add(new Coupon
        {
            Id = couponId, Type = "Future", IsActive = true,
            StartAt  = DateTimeOffset.UtcNow.AddDays(5),   // starts in the future â†’ StartAt > now = true
            EndAt    = DateTimeOffset.UtcNow.AddDays(10),
            CostPoint = 50, CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RedeemCouponAsync(userId, couponId));
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // RewardsService â€” RedeemCouponBySerial uncovered branches
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public async Task RedeemCouponBySerial_SerialNotFound_Throws()
    {
        // Covers RedeemCouponBySerialAsync:
        //   if (userCoupon == null) â†’ TRUE branch (serial doesn't exist in DB).
        var svc = BuildRewards3(out _);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RedeemCouponBySerialAsync("NOTEXIST"));
    }

    [Fact]
    public async Task RedeemCouponBySerial_NotStartedYet_Throws()
    {
        // Covers RedeemCouponBySerialAsync:
        //   if (!userCoupon.Coupon.IsActive)         â†’ FALSE (active)
        //   if (StartAt > now || EndAt < now)        â†’ StartAt > now = TRUE (short-circuit)
        var svc = BuildRewards3(out var db);
        var couponId = Guid.NewGuid();

        db.Coupons.Add(new Coupon
        {
            Id = couponId, Type = "Future2", IsActive = true,
            StartAt  = DateTimeOffset.UtcNow.AddDays(3),  // not started yet
            EndAt    = DateTimeOffset.UtcNow.AddDays(10),
            CostPoint = 0, CreatedAt = DateTimeOffset.UtcNow
        });
        db.UserCoupons.Add(new UserCoupon
        {
            SerialNumber = "FUTURESER", UserId = Guid.NewGuid(),
            CouponId = couponId, IsRedeemed = false, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RedeemCouponBySerialAsync("FUTURESER"));
    }

    [Fact]
    public async Task GetReceiptDetails_MallManagerDifferentMall_Throws()
    {
        // Covers GetReceiptDetailsForUserAsync:
        //   access.IsMallWideManager = true  â†’ evaluates: receipt.StoreMallId == access.MallID
        //   The mall-wide manager is from a DIFFERENT mall â†’ == false â†’ canAccess stays false â†’ throw.
        //   Previous test only covered the true (same mall, success) path.
        var db       = TestInfrastructure.CreateDbContext();
        var mallA    = Guid.NewGuid();   // manager's mall
        var mallB    = Guid.NewGuid();   // receipt's mall (different)
        var userId   = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var storeId  = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile { Id = userId,    Name = "U6", PhoneNumber = "+962799900007", PasswordHash = "x", Role = "user",    MallID = mallB });
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "M6", PhoneNumber = "+962799900008", PasswordHash = "x", Role = "manager", MallID = mallA });
        db.Managers.Add(new Manager { Id = managerId, Name = "M6", MallID = mallA, Role = "manager" });
        db.Stores.Add(new Store { Id = storeId, Name = "SB", MallID = mallB });

        var tx = new Transaction { UserId = userId, StoreId = storeId, ReceiptId = "RX-DIFF", Price = 20, Points = 2000, TransactionStatus = "completed", CreatedAt = DateTimeOffset.UtcNow };
        db.Transactions.Add(tx);
        await db.SaveChangesAsync();

        var svc = new RewardsService(
            db, new PhoneNumberService(), new NoOpUserPointsUpdatesService(),
            new UserAccessService(db, NullLogger<UserAccessService>.Instance));

        await Assert.ThrowsAsync<ApiForbiddenException>(
            () => svc.GetReceiptDetailsForUserAsync(managerId, tx.Id));
    }

    [Fact]
    public async Task GetReceiptDetails_StoreManagerUnassignedStore_Throws()
    {
        // Covers GetReceiptDetailsForUserAsync:
        //   access.IsMallWideManager = false â†’ evaluates: access.AssignedStoreIds.Contains(receipt.StoreId)
        //   The store manager is NOT assigned to the receipt's store â†’ false â†’ canAccess stays false â†’ throw.
        //   Previous test only covered the true (assigned store, success) path.
        var db        = TestInfrastructure.CreateDbContext();
        var mallId    = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var storeA    = Guid.NewGuid();   // manager is assigned here
        var storeB    = Guid.NewGuid();   // receipt is from here

        db.UserProfiles.Add(new UserProfile { Id = userId,    Name = "U7", PhoneNumber = "+962799900009", PasswordHash = "x", Role = "user",    MallID = mallId });
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "SM7", PhoneNumber = "+962799900010", PasswordHash = "x", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = managerId, Name = "SM7", MallID = mallId, Role = "store_manager" });
        db.Stores.Add(new Store { Id = storeA, Name = "SA", MallID = mallId });
        db.Stores.Add(new Store { Id = storeB, Name = "SB", MallID = mallId });
        db.Management.Add(new Management { ManagerId = managerId, StoreId = storeA }); // assigned to storeA only

        var tx = new Transaction { UserId = userId, StoreId = storeB, ReceiptId = "RX-UNASSIGNED", Price = 30, Points = 3000, TransactionStatus = "completed", CreatedAt = DateTimeOffset.UtcNow };
        db.Transactions.Add(tx);
        await db.SaveChangesAsync();

        var svc = new RewardsService(
            db, new PhoneNumberService(), new NoOpUserPointsUpdatesService(),
            new UserAccessService(db, NullLogger<UserAccessService>.Instance));

        await Assert.ThrowsAsync<ApiForbiddenException>(
            () => svc.GetReceiptDetailsForUserAsync(managerId, tx.Id));
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // OffersService â€” store manager creating offer for assigned store
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public async Task CreateOffer_StoreManagerAssignedStore_Succeeds()
    {
        // Covers GetScopedStoreAsync line 165:
        //   if (!access.IsMallWideManager && !access.AssignedStoreIds.Contains(storeId))
        //   â†’ !IsMallWideManager = true, !Contains = false â†’ overall FALSE â†’ no throw.
        //   Previous tests only covered the throw path (not assigned).
        var db     = TestInfrastructure.CreateDbContext();
        var mallId  = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile
        {
            Id = userId, Name = "SM4", PhoneNumber = "+962799900008",
            PasswordHash = "x", Role = "manager", MallID = mallId
        });
        db.Managers.Add(new Manager { Id = userId, Name = "SM4", MallID = mallId, Role = "store_manager" });
        db.Stores.Add(new Store { Id = storeId, Name = "AssignedStore", MallID = mallId });
        db.Management.Add(new Management { ManagerId = userId, StoreId = storeId }); // assigned!
        await db.SaveChangesAsync();

        var svc = new OffersService(
            db,
            new UserAccessService(db, NullLogger<UserAccessService>.Instance),
            NullLogger<OffersService>.Instance);

        var req = new CreateOfferRequest
        {
            StoreId = storeId, Title = "SM Offer",
            StartAt = DateTimeOffset.UtcNow,
            EndAt   = DateTimeOffset.UtcNow.AddDays(7),
            IsActive = true
        };

        var result = await svc.CreateOfferAsync(userId, req);

        Assert.Equal("SM Offer", result.Title);
        Assert.Equal(storeId, result.StoreId);
    }
}

