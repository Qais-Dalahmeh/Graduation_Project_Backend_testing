using Graduation_Project_Backend.DTOs.Announcements;
using Graduation_Project_Backend.DTOs.Auth;
using Graduation_Project_Backend.DTOs.Offers;
using Graduation_Project_Backend.DTOs.Receipts;
using Graduation_Project_Backend.DTOs.Stores;
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
/// Additional branch-coverage tests targeting remaining uncovered branches in
/// AnnouncementsService, StoresService, RewardsService, OffersService, and AuthService.
/// Goal: push branch coverage from 82.5% → 90%+.
/// </summary>
public sealed class BranchCoverageGap2
{
    // ══════════════════════════════════════════════════════════════════
    // AnnouncementsService — remaining uncovered branches
    // ══════════════════════════════════════════════════════════════════

    private static AnnouncementsService BuildAnnouncementsService(
        out Graduation_Project_Backend.Data.AppDbContext db,
        out Guid userId, out Guid mallId,
        bool isMallWide = true)
    {
        db = TestInfrastructure.CreateDbContext();
        mallId = Guid.NewGuid();
        userId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile
        {
            Id = userId, Name = "Mgr", PhoneNumber = "+962799000001",
            PasswordHash = "x", Role = "manager", MallID = mallId
        });
        db.Managers.Add(new Manager { Id = userId, Name = "Mgr", MallID = mallId, Role = "manager" });

        if (!isMallWide)
        {
            var storeId = Guid.NewGuid();
            db.Stores.Add(new Store { Id = storeId, Name = "S", MallID = mallId });
            db.Management.Add(new Management { ManagerId = userId, StoreId = storeId });
        }

        db.SaveChanges();
        var userAccess = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        return new AnnouncementsService(db, userAccess, NullLogger<AnnouncementsService>.Instance);
    }

    [Fact]
    public async Task CreateAnnouncement_WithExplicitTypeAndPriority_UsesProvidedValues()
    {
        // Covers: NormalizeOptional(AnnouncementType) ?? "general" — left side non-null (uses value)
        //     and NormalizeOptional(Priority) ?? "normal"          — left side non-null (uses value)
        var svc = BuildAnnouncementsService(out _, out var userId, out _);
        var req = new CreateAnnouncementRequest
        {
            Title           = "Promo",
            Content         = "Big sale",
            AnnouncementType = "promotion",
            Priority        = "high",
            StartDate       = DateTimeOffset.UtcNow,
            EndDate         = DateTimeOffset.UtcNow.AddDays(5)
        };
        var result = await svc.CreateAnnouncementAsync(userId, req);

        Assert.Equal("promotion", result.AnnouncementType);
        Assert.Equal("high",      result.Priority);
    }

    [Fact]
    public async Task CreateAnnouncement_NullTitle_Throws()
    {
        // Covers: NormalizeRequired → NormalizeOptional(null) returns null
        //         → null ?? string.Empty = "" → IsNullOrWhiteSpace("") = true → throw
        var svc = BuildAnnouncementsService(out _, out var userId, out _);
        var req = new CreateAnnouncementRequest
        {
            Title     = null!,
            Content   = "C",
            StartDate = DateTimeOffset.UtcNow,
            EndDate   = DateTimeOffset.UtcNow.AddDays(1)
        };
        await Assert.ThrowsAsync<ApiValidationException>(() => svc.CreateAnnouncementAsync(userId, req));
    }

    [Fact]
    public async Task CreateAnnouncement_WhitespaceTitle_Throws()
    {
        // Covers: NormalizeRequired → IsNullOrWhiteSpace("   ") = true → throw (different entry)
        var svc = BuildAnnouncementsService(out _, out var userId, out _);
        var req = new CreateAnnouncementRequest
        {
            Title     = "   ",
            Content   = "C",
            StartDate = DateTimeOffset.UtcNow,
            EndDate   = DateTimeOffset.UtcNow.AddDays(1)
        };
        await Assert.ThrowsAsync<ApiValidationException>(() => svc.CreateAnnouncementAsync(userId, req));
    }

    [Fact]
    public async Task CreateAnnouncement_StoreManagerForOwnAssignedStore_Succeeds()
    {
        // Covers: !access.IsMallWideManager && !access.AssignedStoreIds.Contains(store.Id) → FALSE
        //         (store manager IS assigned → no exception thrown)
        var db = TestInfrastructure.CreateDbContext();
        var mallId  = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "SM", PhoneNumber = "+962799000002", PasswordHash = "x", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = userId, Name = "SM", MallID = mallId, Role = "store_manager" });
        db.Stores.Add(new Store { Id = storeId, Name = "MyStore", MallID = mallId });
        db.Management.Add(new Management { ManagerId = userId, StoreId = storeId });
        await db.SaveChangesAsync();

        var svc = new AnnouncementsService(db, new UserAccessService(db, NullLogger<UserAccessService>.Instance), NullLogger<AnnouncementsService>.Instance);
        var req = new CreateAnnouncementRequest
        {
            Title     = "Store Promo",
            Content   = "Come visit!",
            StoreId   = storeId,          // their OWN assigned store
            StartDate = DateTimeOffset.UtcNow,
            EndDate   = DateTimeOffset.UtcNow.AddDays(3)
        };
        var result = await svc.CreateAnnouncementAsync(userId, req);

        Assert.Equal("Store Promo", result.Title);
        Assert.Equal(storeId, result.StoreId);
    }

    [Fact]
    public async Task DeleteAnnouncement_NotFound_Throws()
    {
        // Covers: GetManagedAnnouncementEntityAsync → SingleOrDefault returns null → throw ApiNotFoundException
        var svc = BuildAnnouncementsService(out _, out var userId, out _);
        await Assert.ThrowsAsync<ApiNotFoundException>(() =>
            svc.DeleteAnnouncementAsync(userId, Guid.NewGuid()));
    }

    [Fact]
    public async Task SetAnnouncementPin_NotFound_Throws()
    {
        // Covers: GetManagedAnnouncementEntityAsync null path in SetAnnouncementPinAsync
        var svc = BuildAnnouncementsService(out _, out var userId, out _);
        await Assert.ThrowsAsync<ApiNotFoundException>(() =>
            svc.SetAnnouncementPinAsync(userId, Guid.NewGuid(), true));
    }

    [Fact]
    public async Task SetAnnouncementStatus_NotFound_Throws()
    {
        // Covers: GetManagedAnnouncementEntityAsync null path in SetAnnouncementStatusAsync
        var svc = BuildAnnouncementsService(out _, out var userId, out _);
        await Assert.ThrowsAsync<ApiNotFoundException>(() =>
            svc.SetAnnouncementStatusAsync(userId, Guid.NewGuid(), false));
    }

    [Fact]
    public async Task GetVisibleAnnouncements_MixedPriority_OrdersHighFirst()
    {
        // Covers: announcement.Priority == "high" → true AND false (both ordering branches)
        var db = TestInfrastructure.CreateDbContext();
        var mallId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now    = DateTimeOffset.UtcNow;

        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "U", PhoneNumber = "+962799000003", PasswordHash = "x", Role = "user", MallID = mallId });
        db.Announcements.Add(new Announcement { Id = Guid.NewGuid(), MallID = mallId, Title = "Normal", Content = "x", Priority = "normal", IsActive = true, StartDate = now.AddDays(-1), EndDate = now.AddDays(1) });
        db.Announcements.Add(new Announcement { Id = Guid.NewGuid(), MallID = mallId, Title = "High",   Content = "y", Priority = "high",   IsActive = true, StartDate = now.AddDays(-1), EndDate = now.AddDays(1) });
        await db.SaveChangesAsync();

        var svc    = new AnnouncementsService(db, new UserAccessService(db, NullLogger<UserAccessService>.Instance), NullLogger<AnnouncementsService>.Instance);
        var result = await svc.GetVisibleAnnouncementsAsync(userId);

        Assert.Equal(2, result.Count);
        Assert.Equal("High", result[0].Title); // high priority first
    }

    [Fact]
    public async Task CreateAnnouncement_WithStore_ReturnsStoreName()
    {
        // Covers: store != null ? store.Name : null → non-null branch in BuildAnnouncementProjectionQuery
        var db      = TestInfrastructure.CreateDbContext();
        var mallId  = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "M", PhoneNumber = "+962799000004", PasswordHash = "x", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = userId, Name = "M", MallID = mallId, Role = "manager" });
        db.Stores.Add(new Store { Id = storeId, Name = "CoolStore", MallID = mallId });
        await db.SaveChangesAsync();

        var svc    = new AnnouncementsService(db, new UserAccessService(db, NullLogger<UserAccessService>.Instance), NullLogger<AnnouncementsService>.Instance);
        var req    = new CreateAnnouncementRequest
        {
            Title     = "Store Sale",
            Content   = "50% off!",
            StoreId   = storeId,
            StartDate = DateTimeOffset.UtcNow,
            EndDate   = DateTimeOffset.UtcNow.AddDays(2)
        };
        var result = await svc.CreateAnnouncementAsync(userId, req);

        Assert.Equal("CoolStore", result.StoreName);
    }

    // ══════════════════════════════════════════════════════════════════
    // StoresService — remaining uncovered branches
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetVisibleStoreById_Found_ReturnsStoreResponse()
    {
        // Covers: GetStoreResponseAsync → store != null → return mapped StoreResponse (non-null branch)
        var db      = TestInfrastructure.CreateDbContext();
        var mallId  = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "U", PhoneNumber = "+962799000005", PasswordHash = "x", Role = "user", MallID = mallId });
        db.Stores.Add(new Store { Id = storeId, Name = "FindMe", MallID = mallId });
        await db.SaveChangesAsync();

        var svc    = new StoresService(db, new UserAccessService(db, NullLogger<UserAccessService>.Instance), NullLogger<StoresService>.Instance);
        var result = await svc.GetVisibleStoreByIdAsync(userId, storeId);

        Assert.NotNull(result);
        Assert.Equal("FindMe", result!.Name);
    }

    [Fact]
    public async Task CreateStore_NullCategoryIds_Succeeds()
    {
        // Covers: ValidateCategoryIdsAsync → categoryIds.Count == 0 → early return (no DB query)
        //     and SyncStoreCategoriesAsync  → categoryIds.Count == 0 → early return
        var db     = TestInfrastructure.CreateDbContext();
        var mallId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "M", PhoneNumber = "+962799000006", PasswordHash = "x", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = userId, Name = "M", MallID = mallId, Role = "manager" });
        await db.SaveChangesAsync();

        var svc    = new StoresService(db, new UserAccessService(db, NullLogger<UserAccessService>.Instance), NullLogger<StoresService>.Instance);
        var result = await svc.CreateStoreAsync(userId, new CreateStoreRequest { Name = "NewStore", CategoryIds = null });

        Assert.Equal("NewStore", result.Name);
        Assert.Empty(result.Categories);
    }

    [Fact]
    public async Task UpdateStore_NoExistingCategories_AddsNewCategories()
    {
        // Covers: SyncStoreCategoriesAsync replaceExisting=true → existingCategories.Count == 0
        //         → skip RemoveRange, proceed to add new categories
        var db     = TestInfrastructure.CreateDbContext();
        var mallId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var catId   = 20L;

        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "M", PhoneNumber = "+962799000007", PasswordHash = "x", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = userId, Name = "M", MallID = mallId, Role = "manager" });
        db.Stores.Add(new Store { Id = storeId, Name = "Old", MallID = mallId }); // no existing categories
        db.Categories.Add(new Category { Id = catId, Name = "NewCat", MallID = mallId });
        await db.SaveChangesAsync();

        var svc    = new StoresService(db, new UserAccessService(db, NullLogger<UserAccessService>.Instance), NullLogger<StoresService>.Instance);
        var result = await svc.UpdateStoreAsync(userId, storeId, new UpdateStoreRequest { Name = "Updated", CategoryIds = [catId] });

        Assert.Equal("Updated", result.Name);
        Assert.Single(result.Categories);
        Assert.Equal("NewCat", result.Categories[0].Name);
    }

    [Fact]
    public async Task CreateStore_EmptyName_Throws()
    {
        // Covers: NormalizeRequired in StoresService → IsNullOrWhiteSpace("") = true → throw ApiValidationException
        var db     = TestInfrastructure.CreateDbContext();
        var mallId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "M", PhoneNumber = "+962799000008", PasswordHash = "x", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = userId, Name = "M", MallID = mallId, Role = "manager" });
        await db.SaveChangesAsync();

        var svc = new StoresService(db, new UserAccessService(db, NullLogger<UserAccessService>.Instance), NullLogger<StoresService>.Instance);
        await Assert.ThrowsAsync<ApiValidationException>(
            () => svc.CreateStoreAsync(userId, new CreateStoreRequest { Name = "" }));
    }

    [Fact]
    public async Task GetManagedStores_WithStores_ReturnsList()
    {
        // Covers: GetCategoryNamesByStoreIdAsync with non-empty storeIds (full path)
        var db      = TestInfrastructure.CreateDbContext();
        var mallId  = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "M", PhoneNumber = "+962799000009", PasswordHash = "x", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = userId, Name = "M", MallID = mallId, Role = "manager" });
        db.Stores.Add(new Store { Id = storeId, Name = "BigStore", MallID = mallId });
        await db.SaveChangesAsync();

        var svc    = new StoresService(db, new UserAccessService(db, NullLogger<UserAccessService>.Instance), NullLogger<StoresService>.Instance);
        var result = await svc.GetManagedStoresAsync(userId);

        Assert.Single(result);
        Assert.Equal("BigStore", result[0].Name);
    }

    // ══════════════════════════════════════════════════════════════════
    // RewardsService — remaining uncovered branches
    // ══════════════════════════════════════════════════════════════════

    private static RewardsService BuildRewards(out Graduation_Project_Backend.Data.AppDbContext db)
    {
        db = TestInfrastructure.CreateDbContext();
        return new RewardsService(
            db, new PhoneNumberService(),
            new NoOpUserPointsUpdatesService(),
            new UserAccessService(db, NullLogger<UserAccessService>.Instance));
    }

    [Fact]
    public async Task ProcessTransaction_EmptyStoreId_Throws()
    {
        // Covers: if (storeId == Guid.Empty) → true → throw InvalidOperationException
        var svc = BuildRewards(out _);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ProcessTransactionAsync("+962799000010", Guid.Empty, "RX01", null, 10));
    }

    [Fact]
    public async Task ProcessTransaction_EmptyReceiptId_Throws()
    {
        // Covers: if (string.IsNullOrWhiteSpace(receiptId)) → true → throw
        var svc = BuildRewards(out _);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ProcessTransactionAsync("+962799000011", Guid.NewGuid(), "", null, 10));
    }

    [Fact]
    public async Task ProcessTransaction_EmptyPhoneNumber_Throws()
    {
        // Covers: if (string.IsNullOrWhiteSpace(phoneNumber)) → true → throw
        var svc = BuildRewards(out _);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ProcessTransactionAsync("", Guid.NewGuid(), "RX02", null, 10));
    }

    [Fact]
    public async Task ProcessTransaction_ReceiptAlreadyExists_Throws()
    {
        // Covers: if (await ReceiptExistsAsync(receiptId)) → true → throw
        var svc     = BuildRewards(out var db);
        var mallId  = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var userId  = Guid.NewGuid();

        db.Stores.Add(new Store { Id = storeId, Name = "Shop", MallID = mallId });
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "U", PhoneNumber = "+962799000013", PasswordHash = "x", Role = "user", MallID = mallId });
        db.Transactions.Add(new Transaction { UserId = userId, StoreId = storeId, ReceiptId = "DUP001", Price = 10, Points = 1000, TransactionStatus = "completed", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ProcessTransactionAsync("+962799000013", storeId, "DUP001", null, 10));
    }

    [Fact]
    public async Task ProcessTransaction_Success_EarnsPoints()
    {
        // Covers: all the positive-path branches of ProcessTransactionAsync
        // (phone ok, storeId ok, receiptId ok, price>=0, receipt not exists, mall found, user found)
        var svc     = BuildRewards(out var db);
        var mallId  = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        db.Stores.Add(new Store { Id = storeId, Name = "Shop", MallID = mallId });
        db.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(), Name = "Buyer", PhoneNumber = "+962799000012",
            PasswordHash = "x", Role = "user", MallID = mallId, TotalPoints = 0
        });
        await db.SaveChangesAsync();

        var result = await svc.ProcessTransactionAsync("+962799000012", storeId, "RXSUCCESS01", "desc", 50m);

        Assert.Equal(5000, result.Points);
        Assert.Equal(5000, result.NewTotalPoints);
    }

    [Fact]
    public async Task GetTransactionDetails_ValidTransaction_ReturnsData()
    {
        // Covers: transaction != null → return new { ... } branch (non-null path)
        var svc     = BuildRewards(out var db);
        var userId  = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        db.Stores.Add(new Store { Id = storeId, Name = "S", MallID = Guid.NewGuid() });
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "Buyer", PhoneNumber = "+962799000014", PasswordHash = "x", Role = "user", MallID = Guid.NewGuid() });
        var tx = new Transaction { UserId = userId, StoreId = storeId, ReceiptId = "RDET01", Price = 100, Points = 10000, TransactionStatus = "completed", CreatedAt = DateTimeOffset.UtcNow };
        db.Transactions.Add(tx);
        await db.SaveChangesAsync();

        var result = await svc.GetTransactionDetailsAsync(tx.Id);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetUserTotalPoints_ValidUser_ReturnsPoints()
    {
        // Covers: user?.TotalPoints → non-null (user found) branch
        var svc    = BuildRewards(out var db);
        var userId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "U", PhoneNumber = "+962799000015", PasswordHash = "x", Role = "user", MallID = Guid.NewGuid(), TotalPoints = 250 });
        await db.SaveChangesAsync();

        var result = await svc.GetUserTotalPointsAsync(userId);

        Assert.Equal(250, result);
    }

    [Fact]
    public async Task GetUserTotalPoints_NotFound_ReturnsNull()
    {
        // Covers: user == null → null?.TotalPoints = null branch
        var svc    = BuildRewards(out _);
        var result = await svc.GetUserTotalPointsAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCouponDetails_ValidCoupon_ReturnsData()
    {
        // Covers: coupon != null → return new { ... } branch
        var svc      = BuildRewards(out var db);
        var couponId = Guid.NewGuid();

        db.Coupons.Add(new Coupon { Id = couponId, Type = "Discount", IsActive = true, StartAt = DateTimeOffset.UtcNow.AddDays(-1), EndAt = DateTimeOffset.UtcNow.AddDays(10), CostPoint = 50, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var result = await svc.GetCouponDetailsAsync(couponId);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetCouponDetails_NotFound_ReturnsNull()
    {
        // Covers: coupon == null → return null branch
        var svc    = BuildRewards(out _);
        var result = await svc.GetCouponDetailsAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task RedeemCoupon_CouponNotFound_Throws()
    {
        // Covers: GetCouponAsync → null → throw "Coupon not found"
        var svc    = BuildRewards(out var db);
        var userId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "U", PhoneNumber = "+962799000016", PasswordHash = "x", Role = "user", MallID = Guid.NewGuid(), TotalPoints = 100 });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RedeemCouponAsync(userId, Guid.NewGuid()));
    }

    [Fact]
    public async Task RedeemCouponBySerial_InactiveCoupon_Throws()
    {
        // Covers: userCoupon.Coupon.IsActive == false → throw "Coupon is not active" (in serial path)
        var svc      = BuildRewards(out var db);
        var couponId = Guid.NewGuid();

        db.Coupons.Add(new Coupon { Id = couponId, Type = "X", IsActive = false, StartAt = DateTimeOffset.UtcNow.AddDays(-1), EndAt = DateTimeOffset.UtcNow.AddDays(10), CostPoint = 0, CreatedAt = DateTimeOffset.UtcNow });
        db.UserCoupons.Add(new UserCoupon { SerialNumber = "INACT001", UserId = Guid.NewGuid(), CouponId = couponId, IsRedeemed = false, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RedeemCouponBySerialAsync("INACT001"));
    }

    [Fact]
    public async Task RedeemCouponBySerial_ExpiredCoupon_Throws()
    {
        // Covers: coupon.EndAt < now → throw "Coupon outside redeem period" (in serial path)
        var svc      = BuildRewards(out var db);
        var couponId = Guid.NewGuid();

        db.Coupons.Add(new Coupon { Id = couponId, Type = "X", IsActive = true, StartAt = DateTimeOffset.UtcNow.AddDays(-10), EndAt = DateTimeOffset.UtcNow.AddDays(-1), CostPoint = 0, CreatedAt = DateTimeOffset.UtcNow }); // expired
        db.UserCoupons.Add(new UserCoupon { SerialNumber = "EXP0001", UserId = Guid.NewGuid(), CouponId = couponId, IsRedeemed = false, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RedeemCouponBySerialAsync("EXP0001"));
    }

    [Fact]
    public async Task GetUserCoupons_WithCoupons_ReturnsViewData()
    {
        // Covers: GetUserCouponsViewAsync — uc.Coupon is non-null (Type, Discription, etc.)
        var svc      = BuildRewards(out var db);
        var userId   = Guid.NewGuid();
        var couponId = Guid.NewGuid();

        db.Coupons.Add(new Coupon { Id = couponId, Type = "Voucher", IsActive = true, StartAt = DateTimeOffset.UtcNow.AddDays(-1), EndAt = DateTimeOffset.UtcNow.AddDays(10), CostPoint = 0, CreatedAt = DateTimeOffset.UtcNow });
        db.UserCoupons.Add(new UserCoupon { SerialNumber = "VIEW001", UserId = userId, CouponId = couponId, IsRedeemed = false, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var result = await svc.GetUserCouponsViewAsync(userId);

        Assert.Single(result);
    }

    [Fact]
    public async Task GetMyReceipts_WithNoFilters_ReturnsAll()
    {
        // Covers: all the HasValue == false branches in GetMyReceiptsAsync
        //         (StoreId, Status, From, To are all null → skip those filters)
        var svc     = BuildRewards(out var db);
        var mallId  = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "U", PhoneNumber = "+962799000017", PasswordHash = "x", Role = "user", MallID = mallId });
        db.Stores.Add(new Store { Id = storeId, Name = "S", MallID = mallId });
        db.Transactions.Add(new Transaction { UserId = userId, StoreId = storeId, ReceiptId = "NOFILT01", Price = 20, Points = 2000, TransactionStatus = "completed", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var result = await svc.GetMyReceiptsAsync(userId, new ReceiptListQuery { Page = 1, PageSize = 10 });

        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task DeductPoints_NotEnoughPoints_Throws()
    {
        // Covers: RedeemCouponAsync → coupon.CostPoint.HasValue = true → DeductPoints → not enough → throw
        var svc      = BuildRewards(out var db);
        var userId   = Guid.NewGuid();
        var couponId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "U", PhoneNumber = "+962799000018", PasswordHash = "x", Role = "user", MallID = Guid.NewGuid(), TotalPoints = 10 }); // only 10 points
        db.Coupons.Add(new Coupon { Id = couponId, Type = "X", IsActive = true, StartAt = DateTimeOffset.UtcNow.AddDays(-1), EndAt = DateTimeOffset.UtcNow.AddDays(10), CostPoint = 1000, CreatedAt = DateTimeOffset.UtcNow }); // costs 1000
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RedeemCouponAsync(userId, couponId));
    }

    // ══════════════════════════════════════════════════════════════════
    // OffersService — remaining uncovered branches
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateOffer_NullDescription_ReturnsNullDescription()
    {
        // Covers: NormalizeOptional(request.Description) → null branch (IsNullOrWhiteSpace(null)=true)
        var db      = TestInfrastructure.CreateDbContext();
        var mallId  = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "M", PhoneNumber = "+962799000019", PasswordHash = "x", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = userId, Name = "M", MallID = mallId, Role = "manager" });
        db.Stores.Add(new Store { Id = storeId, Name = "S", MallID = mallId });
        await db.SaveChangesAsync();

        var svc    = new OffersService(db, new UserAccessService(db, NullLogger<UserAccessService>.Instance), NullLogger<OffersService>.Instance);
        var result = await svc.CreateOfferAsync(userId, new CreateOfferRequest
        {
            StoreId     = storeId,
            Title       = "Deal",
            Description = null,
            StartAt     = DateTimeOffset.UtcNow,
            EndAt       = DateTimeOffset.UtcNow.AddDays(2)
        });

        Assert.Equal("Deal", result.Title);
        Assert.Null(result.Description);
    }

    [Fact]
    public async Task GetVisibleOffers_WithActiveOffer_ReturnsOffer()
    {
        // Covers: offer.MallID ?? store.MallID — left side non-null → uses offer.MallID
        var db      = TestInfrastructure.CreateDbContext();
        var mallId  = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var now     = DateTimeOffset.UtcNow;

        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "U", PhoneNumber = "+962799000020", PasswordHash = "x", Role = "user", MallID = mallId });
        db.Stores.Add(new Store { Id = storeId, Name = "Shop", MallID = mallId });
        db.Offers.Add(new Offer { StoreId = storeId, MallID = mallId, Title = "50% Off", IsActive = true, StartAt = now.AddDays(-1), EndAt = now.AddDays(5), MadeAt = now });
        await db.SaveChangesAsync();

        var svc    = new OffersService(db, new UserAccessService(db, NullLogger<UserAccessService>.Instance), NullLogger<OffersService>.Instance);
        var result = await svc.GetVisibleOffersAsync(userId);

        Assert.Single(result);
        Assert.Equal("50% Off", result[0].Title);
    }

    [Fact]
    public async Task SetOfferStatus_NotFound_Throws()
    {
        // Covers: GetManagedOfferEntityAsync → null → throw ApiNotFoundException
        var db     = TestInfrastructure.CreateDbContext();
        var mallId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "M", PhoneNumber = "+962799000021", PasswordHash = "x", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = userId, Name = "M", MallID = mallId, Role = "manager" });
        await db.SaveChangesAsync();

        var svc = new OffersService(db, new UserAccessService(db, NullLogger<UserAccessService>.Instance), NullLogger<OffersService>.Instance);
        await Assert.ThrowsAsync<ApiNotFoundException>(() => svc.SetOfferStatusAsync(userId, 9999L, true));
    }

    [Fact]
    public async Task DeleteOffer_NotFound_Throws()
    {
        // Covers: GetManagedOfferEntityAsync null path in DeleteOfferAsync
        var db     = TestInfrastructure.CreateDbContext();
        var mallId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "M", PhoneNumber = "+962799000022", PasswordHash = "x", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = userId, Name = "M", MallID = mallId, Role = "manager" });
        await db.SaveChangesAsync();

        var svc = new OffersService(db, new UserAccessService(db, NullLogger<UserAccessService>.Instance), NullLogger<OffersService>.Instance);
        await Assert.ThrowsAsync<ApiNotFoundException>(() => svc.DeleteOfferAsync(userId, 9999L));
    }

    // ══════════════════════════════════════════════════════════════════
    // AuthService — remaining uncovered branches
    // ══════════════════════════════════════════════════════════════════

    private static AuthService BuildAuthService(
        out Graduation_Project_Backend.Data.AppDbContext db,
        IPasswordHasher<UserProfile>? hasher = null)
    {
        db = TestInfrastructure.CreateDbContext();
        return new AuthService(
            db,
            new PhoneNumberService(),
            hasher ?? new PasswordHasher<UserProfile>(),
            new SessionService(db));
    }

    /// <summary>A custom hasher that always reports SuccessRehashNeeded on verify.</summary>
    private sealed class AlwaysRehashNeededHasher : IPasswordHasher<UserProfile>
    {
        private readonly PasswordHasher<UserProfile> _inner = new();
        public string HashPassword(UserProfile user, string password) => _inner.HashPassword(user, password);
        public PasswordVerificationResult VerifyHashedPassword(UserProfile user, string hashedPassword, string providedPassword)
            => PasswordVerificationResult.SuccessRehashNeeded;
    }

    [Fact]
    public async Task Login_SuccessRehashNeeded_UpdatesHashAndSucceeds()
    {
        // Covers: verificationResult == SuccessRehashNeeded → true → user.PasswordHash = newHash
        var hasher = new AlwaysRehashNeededHasher();
        var svc    = BuildAuthService(out var db, hasher);
        var mallId = Guid.NewGuid();

        var user = new UserProfile
        {
            Id           = Guid.NewGuid(),
            Name         = "Rehash",
            PhoneNumber  = "+962799000023",
            PasswordHash = new PasswordHasher<UserProfile>().HashPassword(null!, "OldPass123"),
            Role         = "user",
            MallID       = mallId,
            TotalPoints  = 0
        };
        db.UserProfiles.Add(user);
        await db.SaveChangesAsync();

        var dto      = new LoginRequestDto { PhoneNumber = "+962799000023", Password = "OldPass123", MallID = mallId };
        var response = await svc.LoginAsync(dto);

        Assert.NotNull(response.SessionId);
        Assert.Equal("Rehash", response.Name);
    }

    [Fact]
    public async Task ManagerQuickLogin_ExistingUserEmptyPhoneAndRole_FillsDefaults()
    {
        // Covers (else branch when user already exists):
        //   string.IsNullOrWhiteSpace(manager.Role) → true → assign "manager"
        //   string.IsNullOrWhiteSpace(user.PhoneNumber) → true → assign placeholder
        //   string.IsNullOrWhiteSpace(user.PasswordHash) → true → hash new password
        var svc       = BuildAuthService(out var db);
        var managerId = Guid.NewGuid();
        var mallId    = Guid.NewGuid();

        db.Managers.Add(new Manager { Id = managerId, Name = "EmptyRole", MallID = mallId, Role = "" });
        db.UserProfiles.Add(new UserProfile
        {
            Id           = managerId,
            Name         = "OldName",
            PhoneNumber  = "",          // empty → will be filled
            PasswordHash = "",          // empty → will be hashed
            Role         = "manager",
            MallID       = mallId
        });
        await db.SaveChangesAsync();

        var response = await svc.ManagerQuickLoginAsync(new ManagerQuickLoginRequestDto { ManagerId = managerId });

        Assert.NotNull(response.SessionId);
        Assert.Equal("manager", response.Role); // empty role → default "manager"
    }

    [Fact]
    public async Task ManagerQuickLogin_ExistingUserWithNonEmptyRole_UsesManagerRole()
    {
        // Covers (else branch when user already exists):
        //   string.IsNullOrWhiteSpace(manager.Role) → false → use manager.Role.Trim()
        //   string.IsNullOrWhiteSpace(user.PhoneNumber) → false → skip
        //   string.IsNullOrWhiteSpace(user.PasswordHash) → false → skip
        var svc       = BuildAuthService(out var db);
        var managerId = Guid.NewGuid();
        var mallId    = Guid.NewGuid();

        db.Managers.Add(new Manager { Id = managerId, Name = "RoleMgr", MallID = mallId, Role = " admin " });
        db.UserProfiles.Add(new UserProfile
        {
            Id           = managerId,
            Name         = "OldName",
            PhoneNumber  = $"manager-{managerId:N}",  // has phone
            PasswordHash = "existingHash",              // has hash
            Role         = "admin",
            MallID       = mallId
        });
        await db.SaveChangesAsync();

        var response = await svc.ManagerQuickLoginAsync(new ManagerQuickLoginRequestDto { ManagerId = managerId });

        Assert.Equal("admin", response.Role); // " admin ".Trim() = "admin"
    }

    [Fact]
    public async Task Register_ManagerPhoneAlreadyInDifferentMall_Throws()
    {
        // Covers: RegisterManagerAsync → existingUserByPhone != null && MallID != request.MallID
        //         → throw AuthConflictException("PHONE_ALREADY_REGISTERED")
        var svc       = BuildAuthService(out var db);
        var managerId = Guid.NewGuid();
        var mallId    = Guid.NewGuid();
        var otherMall = Guid.NewGuid();

        db.Managers.Add(new Manager { Id = managerId, Name = "Mgr", MallID = mallId, Role = "manager" });
        // Phone already taken in a DIFFERENT mall
        db.UserProfiles.Add(new UserProfile
        {
            Id           = Guid.NewGuid(),
            Name         = "OtherUser",
            PhoneNumber  = "+962799000024",
            PasswordHash = "x",
            Role         = "user",
            MallID       = otherMall
        });
        await db.SaveChangesAsync();

        var dto = new RegisterRequestDto
        {
            ManagerId   = managerId,
            PhoneNumber = "+962799000024",
            Password    = "Pass123",
            MallID      = mallId,
            Name        = "Mgr"
        };
        var ex = await Assert.ThrowsAsync<AuthConflictException>(() => svc.RegisterAsync(dto));
        Assert.Equal("PHONE_ALREADY_REGISTERED", ex.Code);
    }

    [Fact]
    public async Task Register_ExistingUserDifferentMall_Throws()
    {
        // Covers: RegisterAsync → existingUser != null && existingUser.MallID != request.MallID
        //         → throw AuthConflictException("PHONE_ALREADY_REGISTERED")
        var svc    = BuildAuthService(out var db);
        var mallId = Guid.NewGuid();
        var otherMall = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(), Name = "Existing", PhoneNumber = "+962799000025",
            PasswordHash = "x", Role = "user", MallID = otherMall
        });
        await db.SaveChangesAsync();

        var dto = new RegisterRequestDto
        {
            Name        = "New",
            PhoneNumber = "+962799000025",
            Password    = "Pass123",
            MallID      = mallId   // different mall
        };
        var ex = await Assert.ThrowsAsync<AuthConflictException>(() => svc.RegisterAsync(dto));
        Assert.Equal("PHONE_ALREADY_REGISTERED", ex.Code);
    }

    [Fact]
    public async Task Register_ValidateRequest_ManagerIdWithEmptyGuid_Throws()
    {
        // Covers: ValidateRegisterRequest → dto.ManagerId.HasValue && dto.ManagerId.Value == Guid.Empty → throw
        var svc = BuildAuthService(out _);
        var dto = new RegisterRequestDto
        {
            ManagerId   = Guid.Empty,
            PhoneNumber = "+962799000026",
            Password    = "Pass123",
            MallID      = Guid.NewGuid(),
            Name        = "M"
        };
        await Assert.ThrowsAsync<AuthValidationException>(() => svc.RegisterAsync(dto));
    }
}
