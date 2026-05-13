using Graduation_Project_Backend.DTOs.Announcements;
using Graduation_Project_Backend.DTOs.Offers;
using Graduation_Project_Backend.DTOs.Receipts;
using Graduation_Project_Backend.DTOs.Stores;
using Graduation_Project_Backend.Models.Entities;
using Graduation_Project_Backend.Models.User;
using Graduation_Project_Backend.Service;
using Graduation_Project_Backend.Service.Common;
using Graduation_Project_Backend.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Graduation_Project_Backend.Tests.CoverageGapTests;

/// <summary>
/// Targets uncovered branches in AnnouncementsService, OffersService,
/// RewardsService, and StoresService to push branch coverage toward 90%.
/// </summary>
public sealed class BranchCoverageTests_Services
{
    // ══════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════

    private static (AnnouncementsService svc, UserProfile user, Guid userId, Guid mallId) BuildAnnouncementsSetup(bool isManager, bool isMallWide = true)
    {
        var db = TestInfrastructure.CreateDbContext();
        var mallId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile
        {
            Id = userId, Name = "Test", PhoneNumber = "+962791110001",
            PasswordHash = "x", Role = isManager ? "manager" : "user", MallID = mallId
        });

        if (isManager)
        {
            db.Managers.Add(new Manager { Id = userId, Name = "Test", MallID = mallId, Role = "manager" });

            if (!isMallWide)
            {
                var storeId = Guid.NewGuid();
                db.Stores.Add(new Store { Id = storeId, Name = "S", MallID = mallId });
                db.Management.Add(new Management { ManagerId = userId, StoreId = storeId });
            }
        }

        db.SaveChanges();

        var userAccessSvc = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        var svc = new AnnouncementsService(db, userAccessSvc, NullLogger<AnnouncementsService>.Instance);
        var user = db.UserProfiles.Find(userId)!;
        return (svc, user, userId, mallId);
    }

    private static (OffersService svc, Guid userId, Guid mallId, Guid storeId) BuildOffersSetup(bool isMallWide = true)
    {
        var db = TestInfrastructure.CreateDbContext();
        var mallId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile
        {
            Id = userId, Name = "Mgr", PhoneNumber = "+962791110002",
            PasswordHash = "x", Role = "manager", MallID = mallId
        });
        db.Managers.Add(new Manager { Id = userId, Name = "Mgr", MallID = mallId, Role = "manager" });
        db.Stores.Add(new Store { Id = storeId, Name = "Store", MallID = mallId });

        if (!isMallWide)
            db.Management.Add(new Management { ManagerId = userId, StoreId = storeId });

        db.SaveChanges();

        var userAccessSvc = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        var svc = new OffersService(db, userAccessSvc, NullLogger<OffersService>.Instance);
        return (svc, userId, mallId, storeId);
    }

    // ══════════════════════════════════════════════════════════════════
    // AnnouncementsService — branch coverage
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateAnnouncement_InvalidDateRange_Throws()
    {
        var (svc, _, userId, _) = BuildAnnouncementsSetup(isManager: true);
        var req = new CreateAnnouncementRequest
        {
            Title = "T", Content = "C",
            StartDate = DateTimeOffset.UtcNow.AddDays(5),
            EndDate   = DateTimeOffset.UtcNow.AddDays(1) // end < start
        };
        await Assert.ThrowsAsync<ApiValidationException>(() => svc.CreateAnnouncementAsync(userId, req));
    }

    [Fact]
    public async Task CreateAnnouncement_StoreManagerWithNoStoreId_Throws()
    {
        var (svc, _, userId, _) = BuildAnnouncementsSetup(isManager: true, isMallWide: false);
        var req = new CreateAnnouncementRequest
        {
            Title = "T", Content = "C", StoreId = null, // no storeId
            StartDate = DateTimeOffset.UtcNow,
            EndDate   = DateTimeOffset.UtcNow.AddDays(1)
        };
        await Assert.ThrowsAsync<ApiForbiddenException>(() => svc.CreateAnnouncementAsync(userId, req));
    }

    [Fact]
    public async Task CreateAnnouncement_MallWideManagerWithNoStoreId_Succeeds()
    {
        var (svc, _, userId, _) = BuildAnnouncementsSetup(isManager: true, isMallWide: true);
        var req = new CreateAnnouncementRequest
        {
            Title = "Mall Wide", Content = "Content", StoreId = null,
            StartDate = DateTimeOffset.UtcNow,
            EndDate   = DateTimeOffset.UtcNow.AddDays(10)
        };
        var result = await svc.CreateAnnouncementAsync(userId, req);
        Assert.Equal("Mall Wide", result.Title);
    }

    [Fact]
    public async Task CreateAnnouncement_StoreBelongsToDifferentMall_Throws()
    {
        var db = TestInfrastructure.CreateDbContext();
        var mallId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var otherMallId = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "M", PhoneNumber = "+962791110003", PasswordHash = "x", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = userId, Name = "M", MallID = mallId, Role = "manager" });
        db.Stores.Add(new Store { Id = storeId, Name = "Other", MallID = otherMallId }); // different mall
        await db.SaveChangesAsync();

        var svc = new AnnouncementsService(db, new UserAccessService(db, NullLogger<UserAccessService>.Instance), NullLogger<AnnouncementsService>.Instance);
        var req = new CreateAnnouncementRequest
        {
            Title = "T", Content = "C", StoreId = storeId,
            StartDate = DateTimeOffset.UtcNow, EndDate = DateTimeOffset.UtcNow.AddDays(1)
        };
        await Assert.ThrowsAsync<ApiForbiddenException>(() => svc.CreateAnnouncementAsync(userId, req));
    }

    [Fact]
    public async Task CreateAnnouncement_StoreManagerNotAssignedToStore_Throws()
    {
        var db = TestInfrastructure.CreateDbContext();
        var mallId  = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var otherStoreId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "SM", PhoneNumber = "+962791110004", PasswordHash = "x", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = userId, Name = "SM", MallID = mallId, Role = "store_manager" });
        db.Stores.Add(new Store { Id = storeId,      Name = "Assigned", MallID = mallId });
        db.Stores.Add(new Store { Id = otherStoreId, Name = "Other",    MallID = mallId });
        db.Management.Add(new Management { ManagerId = userId, StoreId = storeId }); // assigned to storeId only
        await db.SaveChangesAsync();

        var svc = new AnnouncementsService(db, new UserAccessService(db, NullLogger<UserAccessService>.Instance), NullLogger<AnnouncementsService>.Instance);
        var req = new CreateAnnouncementRequest
        {
            Title = "T", Content = "C", StoreId = otherStoreId, // not assigned
            StartDate = DateTimeOffset.UtcNow, EndDate = DateTimeOffset.UtcNow.AddDays(1)
        };
        await Assert.ThrowsAsync<ApiForbiddenException>(() => svc.CreateAnnouncementAsync(userId, req));
    }

    [Fact]
    public async Task CreateAnnouncement_NonManager_Throws()
    {
        var (svc, _, userId, _) = BuildAnnouncementsSetup(isManager: false);
        var req = new CreateAnnouncementRequest
        {
            Title = "T", Content = "C",
            StartDate = DateTimeOffset.UtcNow, EndDate = DateTimeOffset.UtcNow.AddDays(1)
        };
        await Assert.ThrowsAsync<ApiForbiddenException>(() => svc.CreateAnnouncementAsync(userId, req));
    }

    [Fact]
    public async Task GetManagedAnnouncements_StoreManagerScope_ReturnsOnlyAssignedStore()
    {
        var db = TestInfrastructure.CreateDbContext();
        var mallId   = Guid.NewGuid();
        var userId   = Guid.NewGuid();
        var storeId  = Guid.NewGuid();
        var store2Id = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "SM", PhoneNumber = "+962791110005", PasswordHash = "x", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = userId, Name = "SM", MallID = mallId, Role = "store_manager" });
        db.Stores.Add(new Store { Id = storeId,  Name = "S1", MallID = mallId });
        db.Stores.Add(new Store { Id = store2Id, Name = "S2", MallID = mallId });
        db.Management.Add(new Management { ManagerId = userId, StoreId = storeId });

        db.Announcements.Add(new Announcement { Id = Guid.NewGuid(), MallID = mallId, StoreId = storeId,  Title = "A1", Content = "x", IsActive = true, StartDate = DateTimeOffset.UtcNow, EndDate = DateTimeOffset.UtcNow.AddDays(1) });
        db.Announcements.Add(new Announcement { Id = Guid.NewGuid(), MallID = mallId, StoreId = store2Id, Title = "A2", Content = "x", IsActive = true, StartDate = DateTimeOffset.UtcNow, EndDate = DateTimeOffset.UtcNow.AddDays(1) });
        await db.SaveChangesAsync();

        var svc = new AnnouncementsService(db, new UserAccessService(db, NullLogger<UserAccessService>.Instance), NullLogger<AnnouncementsService>.Instance);
        var result = await svc.GetManagedAnnouncementsAsync(userId);

        Assert.Single(result);
        Assert.Equal("A1", result[0].Title);
    }

    // ══════════════════════════════════════════════════════════════════
    // OffersService — branch coverage
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateOffer_InvalidDateRange_Throws()
    {
        var (svc, userId, _, storeId) = BuildOffersSetup(isMallWide: true);
        var req = new CreateOfferRequest
        {
            StoreId = storeId, Title = "T",
            StartAt = DateTimeOffset.UtcNow.AddDays(5),
            EndAt   = DateTimeOffset.UtcNow.AddDays(1) // end < start
        };
        await Assert.ThrowsAsync<ApiValidationException>(() => svc.CreateOfferAsync(userId, req));
    }

    [Fact]
    public async Task CreateOffer_StoreDifferentMall_Throws()
    {
        var db = TestInfrastructure.CreateDbContext();
        var mallId  = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "M", PhoneNumber = "+962791110006", PasswordHash = "x", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = userId, Name = "M", MallID = mallId, Role = "manager" });
        db.Stores.Add(new Store { Id = storeId, Name = "X", MallID = Guid.NewGuid() }); // different mall
        await db.SaveChangesAsync();

        var svc = new OffersService(db, new UserAccessService(db, NullLogger<UserAccessService>.Instance), NullLogger<OffersService>.Instance);
        var req = new CreateOfferRequest { StoreId = storeId, Title = "T", StartAt = DateTimeOffset.UtcNow, EndAt = DateTimeOffset.UtcNow.AddDays(1) };
        await Assert.ThrowsAsync<ApiForbiddenException>(() => svc.CreateOfferAsync(userId, req));
    }

    [Fact]
    public async Task CreateOffer_StoreManagerNotAssigned_Throws()
    {
        var db = TestInfrastructure.CreateDbContext();
        var mallId       = Guid.NewGuid();
        var userId       = Guid.NewGuid();
        var storeId      = Guid.NewGuid();
        var otherStoreId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "SM", PhoneNumber = "+962791110007", PasswordHash = "x", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = userId, Name = "SM", MallID = mallId, Role = "store_manager" });
        db.Stores.Add(new Store { Id = storeId,      Name = "Assigned", MallID = mallId });
        db.Stores.Add(new Store { Id = otherStoreId, Name = "Other",    MallID = mallId });
        db.Management.Add(new Management { ManagerId = userId, StoreId = storeId });
        await db.SaveChangesAsync();

        var svc = new OffersService(db, new UserAccessService(db, NullLogger<UserAccessService>.Instance), NullLogger<OffersService>.Instance);
        var req = new CreateOfferRequest { StoreId = otherStoreId, Title = "T", StartAt = DateTimeOffset.UtcNow, EndAt = DateTimeOffset.UtcNow.AddDays(1) };
        await Assert.ThrowsAsync<ApiForbiddenException>(() => svc.CreateOfferAsync(userId, req));
    }

    [Fact]
    public async Task GetManagedOffers_StoreManagerScope_ReturnsOnlyAssignedStoreOffers()
    {
        var db = TestInfrastructure.CreateDbContext();
        var mallId       = Guid.NewGuid();
        var userId       = Guid.NewGuid();
        var storeId      = Guid.NewGuid();
        var otherStoreId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "SM", PhoneNumber = "+962791110008", PasswordHash = "x", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = userId, Name = "SM", MallID = mallId, Role = "store_manager" });
        db.Stores.Add(new Store { Id = storeId,      Name = "S1", MallID = mallId });
        db.Stores.Add(new Store { Id = otherStoreId, Name = "S2", MallID = mallId });
        db.Management.Add(new Management { ManagerId = userId, StoreId = storeId });
        db.Offers.Add(new Offer { StoreId = storeId,      MallID = mallId, Title = "O1", StartAt = DateTimeOffset.UtcNow, EndAt = DateTimeOffset.UtcNow.AddDays(1), IsActive = true });
        db.Offers.Add(new Offer { StoreId = otherStoreId, MallID = mallId, Title = "O2", StartAt = DateTimeOffset.UtcNow, EndAt = DateTimeOffset.UtcNow.AddDays(1), IsActive = true });
        await db.SaveChangesAsync();

        var svc = new OffersService(db, new UserAccessService(db, NullLogger<UserAccessService>.Instance), NullLogger<OffersService>.Instance);
        var result = await svc.GetManagedOffersAsync(userId);

        Assert.Single(result);
        Assert.Equal("O1", result[0].Title);
    }

    // ══════════════════════════════════════════════════════════════════
    // RewardsService — branch coverage
    // ══════════════════════════════════════════════════════════════════

    private static RewardsService BuildRewardsService(out Graduation_Project_Backend.Data.AppDbContext db)
    {
        db = TestInfrastructure.CreateDbContext();
        var userAccessSvc = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        return new RewardsService(db, new PhoneNumberService(), new NoOpUserPointsUpdatesService(), userAccessSvc);
    }

    [Fact]
    public async Task GetCoupons_WithIsActiveFilter_FiltersCorrectly()
    {
        var svc = BuildRewardsService(out var db);
        db.Coupons.Add(new Coupon { Id = Guid.NewGuid(), Type = "A", IsActive = true,  StartAt = DateTimeOffset.UtcNow.AddDays(-1), EndAt = DateTimeOffset.UtcNow.AddDays(10), CostPoint = 100, CreatedAt = DateTimeOffset.UtcNow });
        db.Coupons.Add(new Coupon { Id = Guid.NewGuid(), Type = "B", IsActive = false, StartAt = DateTimeOffset.UtcNow.AddDays(-1), EndAt = DateTimeOffset.UtcNow.AddDays(10), CostPoint = 100, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var active   = await svc.GetCouponsAsync(isActive: true);
        var inactive = await svc.GetCouponsAsync(isActive: false);
        var all      = await svc.GetCouponsAsync(isActive: null);

        Assert.All(active,   c => Assert.True(c.IsActive));
        Assert.All(inactive, c => Assert.False(c.IsActive));
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task GetReceiptDetails_NullReceipt_ReturnsNull()
    {
        var svc = BuildRewardsService(out var db);
        var mallId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "U", PhoneNumber = "+962791110009", PasswordHash = "x", Role = "user", MallID = mallId });
        await db.SaveChangesAsync();

        var result = await svc.GetReceiptDetailsForUserAsync(userId, 99999);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetReceiptDetails_OwnReceipt_ReturnsDetails()
    {
        var svc = BuildRewardsService(out var db);
        var mallId  = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "U", PhoneNumber = "+962791110010", PasswordHash = "x", Role = "user", MallID = mallId });
        db.Stores.Add(new Store { Id = storeId, Name = "S", MallID = mallId });
        var tx = new Transaction { UserId = userId, StoreId = storeId, ReceiptId = "R1", Price = 50, Points = 5000, TransactionStatus = "completed", CreatedAt = DateTimeOffset.UtcNow };
        db.Transactions.Add(tx);
        await db.SaveChangesAsync();

        var result = await svc.GetReceiptDetailsForUserAsync(userId, tx.Id);
        Assert.NotNull(result);
        Assert.Equal("R1", result!.ReceiptId);
    }

    [Fact]
    public async Task GetReceiptDetails_ManagerMallWideAccess_ReturnsDetails()
    {
        var db = TestInfrastructure.CreateDbContext();
        var mallId   = Guid.NewGuid();
        var userId   = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var storeId  = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile { Id = userId,    Name = "U", PhoneNumber = "+962791110011", PasswordHash = "x", Role = "user",    MallID = mallId });
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "M", PhoneNumber = "+962791110012", PasswordHash = "x", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = managerId, Name = "M", MallID = mallId, Role = "manager" });
        db.Stores.Add(new Store { Id = storeId, Name = "S", MallID = mallId });
        var tx = new Transaction { UserId = userId, StoreId = storeId, ReceiptId = "R2", Price = 50, Points = 5000, TransactionStatus = "completed", CreatedAt = DateTimeOffset.UtcNow };
        db.Transactions.Add(tx);
        await db.SaveChangesAsync();

        var userAccessSvc = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        var svc = new RewardsService(db, new PhoneNumberService(), new NoOpUserPointsUpdatesService(), userAccessSvc);

        // manager (mall-wide) accesses a receipt that belongs to userId
        var result = await svc.GetReceiptDetailsForUserAsync(managerId, tx.Id);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetReceiptDetails_StoreManagerAssignedStore_ReturnsDetails()
    {
        var db = TestInfrastructure.CreateDbContext();
        var mallId    = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var storeId   = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile { Id = userId,    Name = "U",  PhoneNumber = "+962791110013", PasswordHash = "x", Role = "user",    MallID = mallId });
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "SM", PhoneNumber = "+962791110014", PasswordHash = "x", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = managerId, Name = "SM", MallID = mallId, Role = "store_manager" });
        db.Stores.Add(new Store { Id = storeId, Name = "S", MallID = mallId });
        db.Management.Add(new Management { ManagerId = managerId, StoreId = storeId });
        var tx = new Transaction { UserId = userId, StoreId = storeId, ReceiptId = "R3", Price = 50, Points = 5000, TransactionStatus = "completed", CreatedAt = DateTimeOffset.UtcNow };
        db.Transactions.Add(tx);
        await db.SaveChangesAsync();

        var svc = new RewardsService(db, new PhoneNumberService(), new NoOpUserPointsUpdatesService(), new UserAccessService(db, NullLogger<UserAccessService>.Instance));
        var result = await svc.GetReceiptDetailsForUserAsync(managerId, tx.Id);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetReceiptDetails_UnrelatedUser_Throws()
    {
        var db = TestInfrastructure.CreateDbContext();
        var mallId  = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile { Id = userId,  Name = "U", PhoneNumber = "+962791110015", PasswordHash = "x", Role = "user", MallID = mallId });
        db.UserProfiles.Add(new UserProfile { Id = otherId, Name = "O", PhoneNumber = "+962791110016", PasswordHash = "x", Role = "user", MallID = mallId });
        db.Stores.Add(new Store { Id = storeId, Name = "S", MallID = mallId });
        var tx = new Transaction { UserId = userId, StoreId = storeId, ReceiptId = "R4", Price = 10, Points = 1000, TransactionStatus = "completed", CreatedAt = DateTimeOffset.UtcNow };
        db.Transactions.Add(tx);
        await db.SaveChangesAsync();

        var svc = new RewardsService(db, new PhoneNumberService(), new NoOpUserPointsUpdatesService(), new UserAccessService(db, NullLogger<UserAccessService>.Instance));
        await Assert.ThrowsAsync<ApiForbiddenException>(() => svc.GetReceiptDetailsForUserAsync(otherId, tx.Id));
    }

    [Fact]
    public async Task GetMyReceipts_DateFilter_InvalidRange_Throws()
    {
        var svc = BuildRewardsService(out var db);
        var userId = Guid.NewGuid();
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "U", PhoneNumber = "+962791110017", PasswordHash = "x", Role = "user", MallID = Guid.NewGuid() });
        await db.SaveChangesAsync();

        var query = new ReceiptListQuery
        {
            Page = 1, PageSize = 10,
            From = DateTimeOffset.UtcNow.AddDays(5),
            To   = DateTimeOffset.UtcNow.AddDays(1) // To < From
        };
        await Assert.ThrowsAsync<ApiValidationException>(() => svc.GetMyReceiptsAsync(userId, query));
    }

    [Fact]
    public async Task GetMyReceipts_AllFilters_Applied()
    {
        var svc = BuildRewardsService(out var db);
        var mallId  = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "U", PhoneNumber = "+962791110018", PasswordHash = "x", Role = "user", MallID = mallId });
        db.Stores.Add(new Store { Id = storeId, Name = "S", MallID = mallId });
        db.Transactions.Add(new Transaction { UserId = userId, StoreId = storeId, ReceiptId = "RF1", Price = 100, Points = 10000, TransactionStatus = "completed", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var query = new ReceiptListQuery
        {
            Page = 1, PageSize = 10,
            StoreId = storeId,
            Status  = "completed",
            From    = DateTimeOffset.UtcNow.AddDays(-1),
            To      = DateTimeOffset.UtcNow.AddDays(1)
        };
        var result = await svc.GetMyReceiptsAsync(userId, query);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task RedeemCoupon_InactiveCoupon_Throws()
    {
        var svc = BuildRewardsService(out var db);
        var userId   = Guid.NewGuid();
        var couponId = Guid.NewGuid();
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "U", PhoneNumber = "+962791110019", PasswordHash = "x", Role = "user", MallID = Guid.NewGuid(), TotalPoints = 9999 });
        db.Coupons.Add(new Coupon { Id = couponId, Type = "X", IsActive = false, StartAt = DateTimeOffset.UtcNow.AddDays(-1), EndAt = DateTimeOffset.UtcNow.AddDays(10), CostPoint = 100, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RedeemCouponAsync(userId, couponId));
    }

    [Fact]
    public async Task RedeemCoupon_ExpiredCoupon_Throws()
    {
        var svc = BuildRewardsService(out var db);
        var userId   = Guid.NewGuid();
        var couponId = Guid.NewGuid();
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "U", PhoneNumber = "+962791110020", PasswordHash = "x", Role = "user", MallID = Guid.NewGuid(), TotalPoints = 9999 });
        db.Coupons.Add(new Coupon { Id = couponId, Type = "X", IsActive = true, StartAt = DateTimeOffset.UtcNow.AddDays(-10), EndAt = DateTimeOffset.UtcNow.AddDays(-1), CostPoint = 100, CreatedAt = DateTimeOffset.UtcNow }); // expired
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RedeemCouponAsync(userId, couponId));
    }

    [Fact]
    public async Task RedeemCoupon_NoCostPoint_SucceedsWithoutDeducting()
    {
        var svc = BuildRewardsService(out var db);
        var userId   = Guid.NewGuid();
        var couponId = Guid.NewGuid();
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "U", PhoneNumber = "+962791110021", PasswordHash = "x", Role = "user", MallID = Guid.NewGuid(), TotalPoints = 100 });
        db.Coupons.Add(new Coupon { Id = couponId, Type = "Free", IsActive = true, StartAt = DateTimeOffset.UtcNow.AddDays(-1), EndAt = DateTimeOffset.UtcNow.AddDays(10), CostPoint = null, CreatedAt = DateTimeOffset.UtcNow }); // no cost
        await db.SaveChangesAsync();

        var result = await svc.RedeemCouponAsync(userId, couponId);
        Assert.NotNull(result.SerialNumber);
        Assert.Equal(100, db.UserProfiles.Find(userId)!.TotalPoints); // unchanged
    }

    [Fact]
    public async Task RedeemCouponBySerial_AlreadyRedeemed_Throws()
    {
        var svc = BuildRewardsService(out var db);
        var couponId = Guid.NewGuid();
        db.Coupons.Add(new Coupon { Id = couponId, Type = "X", IsActive = true, StartAt = DateTimeOffset.UtcNow.AddDays(-1), EndAt = DateTimeOffset.UtcNow.AddDays(10), CostPoint = 0, CreatedAt = DateTimeOffset.UtcNow });
        db.UserCoupons.Add(new UserCoupon { SerialNumber = "USED0001", UserId = Guid.NewGuid(), CouponId = couponId, IsRedeemed = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RedeemCouponBySerialAsync("USED0001"));
    }

    [Fact]
    public async Task RedeemCouponBySerial_EmptySerial_Throws()
    {
        var svc = BuildRewardsService(out var db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RedeemCouponBySerialAsync(""));
    }

    [Fact]
    public async Task GetTransactionDetails_NullTransaction_ReturnsNull()
    {
        var svc = BuildRewardsService(out var db);
        var result = await svc.GetTransactionDetailsAsync(99999);
        Assert.Null(result);
    }

    [Fact]
    public async Task ProcessTransaction_NegativePrice_Throws()
    {
        var svc = BuildRewardsService(out var db);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ProcessTransactionAsync("+962791110022", Guid.NewGuid(), "R99", null, -1));
    }

    // ══════════════════════════════════════════════════════════════════
    // StoresService — branch coverage
    // ══════════════════════════════════════════════════════════════════

    private static StoresService BuildStoresService(out Graduation_Project_Backend.Data.AppDbContext db)
    {
        db = TestInfrastructure.CreateDbContext();
        var userAccessSvc = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        return new StoresService(db, userAccessSvc, NullLogger<StoresService>.Instance);
    }

    [Fact]
    public async Task GetVisibleStoreById_NotFound_ReturnsNull()
    {
        var db = TestInfrastructure.CreateDbContext();
        var mallId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "U", PhoneNumber = "+962791110023", PasswordHash = "x", Role = "user", MallID = mallId });
        await db.SaveChangesAsync();

        var svc = new StoresService(db, new UserAccessService(db, NullLogger<UserAccessService>.Instance), NullLogger<StoresService>.Instance);
        var result = await svc.GetVisibleStoreByIdAsync(userId, Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateStore_StoreManagerAccess_Throws()
    {
        var db = TestInfrastructure.CreateDbContext();
        var mallId  = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "SM", PhoneNumber = "+962791110024", PasswordHash = "x", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = userId, Name = "SM", MallID = mallId, Role = "store_manager" });
        db.Stores.Add(new Store { Id = storeId, Name = "S", MallID = mallId });
        db.Management.Add(new Management { ManagerId = userId, StoreId = storeId }); // assigned → not mall-wide
        await db.SaveChangesAsync();

        var svc = new StoresService(db, new UserAccessService(db, NullLogger<UserAccessService>.Instance), NullLogger<StoresService>.Instance);
        await Assert.ThrowsAsync<ApiForbiddenException>(
            () => svc.CreateStoreAsync(userId, new CreateStoreRequest { Name = "New" }));
    }

    [Fact]
    public async Task CreateStore_NonManagerAccess_Throws()
    {
        var db = TestInfrastructure.CreateDbContext();
        var mallId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "U", PhoneNumber = "+962791110025", PasswordHash = "x", Role = "user", MallID = mallId });
        await db.SaveChangesAsync();

        var svc = new StoresService(db, new UserAccessService(db, NullLogger<UserAccessService>.Instance), NullLogger<StoresService>.Instance);
        await Assert.ThrowsAsync<ApiForbiddenException>(
            () => svc.CreateStoreAsync(userId, new CreateStoreRequest { Name = "New" }));
    }

    [Fact]
    public async Task CreateStore_InvalidCategoryIds_Throws()
    {
        var db = TestInfrastructure.CreateDbContext();
        var mallId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "M", PhoneNumber = "+962791110026", PasswordHash = "x", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = userId, Name = "M", MallID = mallId, Role = "manager" });
        await db.SaveChangesAsync();

        var svc = new StoresService(db, new UserAccessService(db, NullLogger<UserAccessService>.Instance), NullLogger<StoresService>.Instance);
        await Assert.ThrowsAsync<ApiValidationException>(
            () => svc.CreateStoreAsync(userId, new CreateStoreRequest { Name = "S", CategoryIds = [999L] })); // nonexistent category
    }

    [Fact]
    public async Task UpdateStore_WithExistingCategories_ReplacesCategories()
    {
        var db = TestInfrastructure.CreateDbContext();
        var mallId   = Guid.NewGuid();
        var userId   = Guid.NewGuid();
        var storeId  = Guid.NewGuid();
        var cat1Id   = 1L;
        var cat2Id   = 2L;

        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "M", PhoneNumber = "+962791110027", PasswordHash = "x", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = userId, Name = "M", MallID = mallId, Role = "manager" });
        db.Stores.Add(new Store { Id = storeId, Name = "Old", MallID = mallId });
        db.Categories.Add(new Category { Id = cat1Id, Name = "Cat1", MallID = mallId });
        db.Categories.Add(new Category { Id = cat2Id, Name = "Cat2", MallID = mallId });
        db.StoreCategories.Add(new StoreCategory { StoreId = storeId, CategoryId = cat1Id }); // existing
        await db.SaveChangesAsync();

        var svc = new StoresService(db, new UserAccessService(db, NullLogger<UserAccessService>.Instance), NullLogger<StoresService>.Instance);
        var result = await svc.UpdateStoreAsync(userId, storeId, new UpdateStoreRequest { Name = "New", CategoryIds = [cat2Id] });

        Assert.Equal("New", result.Name);
        Assert.Single(result.Categories);
        Assert.Equal("Cat2", result.Categories[0].Name);
    }

    [Fact]
    public async Task GetCategoriesByStoreId_EmptyList_ReturnsEmpty()
    {
        var db = TestInfrastructure.CreateDbContext();
        var mallId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "U", PhoneNumber = "+962791110028", PasswordHash = "x", Role = "user", MallID = mallId });
        await db.SaveChangesAsync();

        var svc = new StoresService(db, new UserAccessService(db, NullLogger<UserAccessService>.Instance), NullLogger<StoresService>.Instance);
        // GetVisibleStoresAsync with no stores → hits storeIds.Count == 0 branch
        var result = await svc.GetVisibleStoresAsync(userId);
        Assert.Empty(result);
    }
}
