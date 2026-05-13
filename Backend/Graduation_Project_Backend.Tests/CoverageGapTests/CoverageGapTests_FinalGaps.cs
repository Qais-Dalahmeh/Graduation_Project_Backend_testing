using System.Text.Json;
using Graduation_Project_Backend.DTOs.Announcements;
using Graduation_Project_Backend.DTOs.Auth;
using Graduation_Project_Backend.DTOs.Offers;
using Graduation_Project_Backend.DTOs.Receipts;
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
/// Final coverage-gap tests targeting the remaining uncovered lines across:
/// OffersService, ApiExceptions, PhoneNumberService, JsonDocumentMapper,
/// AuthService, RewardsService.GetReceiptDetailsForUserAsync,
/// AnnouncementsService.ValidateAnnouncementRequestAsync,
/// StoresService.ValidateCategoryIdsAsync, UserAccessService.
/// </summary>
public sealed class CoverageGapTests_FinalGaps
{
    // ── Exception classes (just need one instantiation each) ─────────────────

    [Fact]
    public void ApiUnauthorizedException_CanBeInstantiated()
    {
        var ex = new ApiUnauthorizedException("test", "TEST_CODE");
        Assert.Equal("TEST_CODE", ex.Code);
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public void ApiConflictException_CanBeInstantiated()
    {
        var ex = new ApiConflictException("conflict", "CONFLICT_CODE");
        Assert.Equal("CONFLICT_CODE", ex.Code);
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public void ApiExternalServiceException_CanBeInstantiated()
    {
        var ex = new ApiExternalServiceException("external error");
        Assert.Equal("EXTERNAL_SERVICE_ERROR", ex.Code);
        Assert.Equal(502, ex.StatusCode);
    }

    // ── JsonDocumentMapper ───────────────────────────────────────────────────

    [Fact]
    public void ToJsonDocument_ReturnsNull_ForNullInput()
    {
        var result = JsonDocumentMapper.ToJsonDocument(null);
        Assert.Null(result);
    }

    [Fact]
    public void ToJsonDocument_ReturnsNull_ForNullJsonElement()
    {
        JsonElement? element = JsonSerializer.Deserialize<JsonElement?>("null");
        var result = JsonDocumentMapper.ToJsonDocument(element);
        Assert.Null(result);
    }

    [Fact]
    public void ToJsonDocument_ReturnsDocument_ForValidJsonElement()
    {
        JsonElement element = JsonSerializer.Deserialize<JsonElement>("{\"key\":\"value\"}");
        var result = JsonDocumentMapper.ToJsonDocument(element);
        Assert.NotNull(result);
    }

    [Fact]
    public void ToJsonElement_ReturnsNull_ForNullDocument()
    {
        var result = JsonDocumentMapper.ToJsonElement(null);
        Assert.Null(result);
    }

    [Fact]
    public void ToJsonElement_ReturnsElement_ForValidDocument()
    {
        using var doc = JsonDocument.Parse("{\"x\":1}");
        var result = JsonDocumentMapper.ToJsonElement(doc);
        Assert.NotNull(result);
    }

    // ── PhoneNumberService ───────────────────────────────────────────────────

    [Fact]
    public void PhoneNumberService_Normalizes_07Format()
    {
        var svc = new PhoneNumberService();
        var result = svc.Normalize("0799000001");
        Assert.Equal("+962799000001", result);
    }

    [Fact]
    public void PhoneNumberService_Normalizes_962Format_WithoutPlus()
    {
        var svc = new PhoneNumberService();
        var result = svc.Normalize("962799000002");
        Assert.Equal("+962799000002", result);
    }

    [Fact]
    public void PhoneNumberService_Throws_ForNull()
    {
        var svc = new PhoneNumberService();
        Assert.Throws<ArgumentException>(() => svc.Normalize(""));
    }

    [Fact]
    public void PhoneNumberService_Throws_ForInvalidChars()
    {
        var svc = new PhoneNumberService();
        Assert.Throws<ArgumentException>(() => svc.Normalize("+962abc00001"));
    }

    [Fact]
    public void PhoneNumberService_Throws_ForNonJordanian_Plus()
    {
        var svc = new PhoneNumberService();
        Assert.Throws<ArgumentException>(() => svc.Normalize("+1234567890"));
    }

    [Fact]
    public void PhoneNumberService_Throws_ForInvalidLength_Plus()
    {
        var svc = new PhoneNumberService();
        Assert.Throws<ArgumentException>(() => svc.Normalize("+96279900000"));  // 11 digits
    }

    [Fact]
    public void PhoneNumberService_Throws_For07_WithWrongLength()
    {
        var svc = new PhoneNumberService();
        Assert.Throws<ArgumentException>(() => svc.Normalize("07990000"));  // too short
    }

    [Fact]
    public void PhoneNumberService_Throws_ForNon7_After962()
    {
        var svc = new PhoneNumberService();
        Assert.Throws<ArgumentException>(() => svc.Normalize("+962600000001"));  // 6 not 7
    }

    [Fact]
    public void PhoneNumberService_Throws_ForNon7_After962_NoPlus()
    {
        var svc = new PhoneNumberService();
        Assert.Throws<ArgumentException>(() => svc.Normalize("962600000001"));  // 6 not 7
    }

    [Fact]
    public void PhoneNumberService_Throws_ForWrongLength_962_NoPlus()
    {
        var svc = new PhoneNumberService();
        Assert.Throws<ArgumentException>(() => svc.Normalize("96279900000"));  // 11 digits
    }

    [Fact]
    public void PhoneNumberService_Throws_ForUnknownFormat()
    {
        var svc = new PhoneNumberService();
        Assert.Throws<ArgumentException>(() => svc.Normalize("1234567890"));  // no +, not 07, not 962
    }

    // ── OffersService.GetOffersAsync & GetManagedOffersAsync ─────────────────

    private static OffersService CreateOffersService(AppDbContext db)
    {
        var access = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        return new OffersService(db, access, NullLogger<OffersService>.Instance);
    }

    [Fact]
    public async Task GetOffersAsync_ReturnsAllOffers()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid storeId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = now });
        db.Stores.Add(new Store { Id = storeId, Name = "Store", MallID = mallId });
        db.Offers.AddRange(
            new Offer { Id = 10L, StoreId = storeId, MallID = mallId, Title = "A", StartAt = now.AddDays(-1), EndAt = now.AddDays(5), IsActive = true, MadeAt = now },
            new Offer { Id = 11L, StoreId = storeId, MallID = mallId, Title = "B", StartAt = now.AddDays(-2), EndAt = now.AddDays(3), IsActive = false, MadeAt = now.AddDays(-1) });
        await db.SaveChangesAsync();

        var service = CreateOffersService(db);
        var result = await service.GetOffersAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetManagedOffersAsync_MallWideManager_ReturnsAllMallOffers()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();
        Guid storeId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = now });
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Mgr", PhoneNumber = "+962700800001", PasswordHash = "h", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = managerId, Name = "Mgr", Role = "manager", MallID = mallId });
        db.Stores.Add(new Store { Id = storeId, Name = "Store", MallID = mallId });
        db.Offers.Add(new Offer { Id = 20L, StoreId = storeId, MallID = mallId, Title = "Offer", StartAt = now.AddDays(-1), EndAt = now.AddDays(5), IsActive = true, MadeAt = now });
        await db.SaveChangesAsync();

        var service = CreateOffersService(db);
        var result = await service.GetManagedOffersAsync(managerId);

        Assert.Single(result);
        Assert.Equal("Offer", result[0].Title);
    }

    [Fact]
    public async Task GetManagedOffersAsync_StoreScopedManager_ReturnsOnlyScopedOffers()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();
        Guid storeId = Guid.NewGuid();
        Guid otherStoreId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = now });
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Mgr", PhoneNumber = "+962700800002", PasswordHash = "h", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = managerId, Name = "Mgr", Role = "manager", MallID = mallId });
        db.Stores.AddRange(
            new Store { Id = storeId, Name = "My Store", MallID = mallId },
            new Store { Id = otherStoreId, Name = "Other", MallID = mallId });
        db.Management.Add(new Management { ManagerId = managerId, StoreId = storeId, CreatedAt = now });
        db.Offers.AddRange(
            new Offer { Id = 30L, StoreId = storeId, MallID = mallId, Title = "Mine", StartAt = now.AddDays(-1), EndAt = now.AddDays(5), IsActive = true, MadeAt = now },
            new Offer { Id = 31L, StoreId = otherStoreId, MallID = mallId, Title = "NotMine", StartAt = now.AddDays(-1), EndAt = now.AddDays(5), IsActive = true, MadeAt = now });
        await db.SaveChangesAsync();

        var service = CreateOffersService(db);
        var result = await service.GetManagedOffersAsync(managerId);

        Assert.Single(result);
        Assert.Equal("Mine", result[0].Title);
    }

    // ── OffersService.UpdateOfferAsync ───────────────────────────────────────

    [Fact]
    public async Task UpdateOfferAsync_UpdatesTitleAndDates()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();
        Guid storeId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = now });
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Mgr", PhoneNumber = "+962700800010", PasswordHash = "h", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = managerId, Name = "Mgr", Role = "manager", MallID = mallId });
        db.Stores.Add(new Store { Id = storeId, Name = "Store", MallID = mallId });
        db.Offers.Add(new Offer { Id = 40L, StoreId = storeId, MallID = mallId, Title = "Old Title", StartAt = now.AddDays(-1), EndAt = now.AddDays(3), IsActive = true, MadeAt = now });
        await db.SaveChangesAsync();

        var service = CreateOffersService(db);
        var request = new UpdateOfferRequest
        {
            StoreId = storeId,
            Title = "New Title",
            StartAt = now.AddDays(-1),
            EndAt = now.AddDays(10),
            IsActive = true
        };

        var result = await service.UpdateOfferAsync(managerId, 40L, request);

        Assert.Equal("New Title", result.Title);
    }

    // ── RewardsService.GetReceiptDetailsForUserAsync — manager path ──────────

    [Fact]
    public async Task GetReceiptDetailsForUserAsync_MallWideManager_CanViewAnyReceipt()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();
        Guid customerId = Guid.NewGuid();
        Guid storeId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = now });
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Mgr", PhoneNumber = "+962700800020", PasswordHash = "h", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = managerId, Name = "Mgr", Role = "manager", MallID = mallId });
        db.UserProfiles.Add(new UserProfile { Id = customerId, Name = "Customer", PhoneNumber = "+962700800021", PasswordHash = "h", Role = "user", MallID = mallId });
        db.Stores.Add(new Store { Id = storeId, Name = "Store", MallID = mallId });
        db.Transactions.Add(new Transaction { Id = 500, UserId = customerId, StoreId = storeId, ReceiptId = "MR1", Price = 100, Points = 10000, CreatedAt = now, TransactionStatus = "completed" });
        await db.SaveChangesAsync();

        var access = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        var svc = new RewardsService(db, new PhoneNumberService(), new NoOpUserPointsUpdatesService(), access);

        var result = await svc.GetReceiptDetailsForUserAsync(managerId, 500);

        Assert.NotNull(result);
        Assert.Equal("MR1", result.ReceiptId);
    }

    [Fact]
    public async Task GetReceiptDetailsForUserAsync_StoreScopedManager_CanViewAssignedStoreReceipt()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();
        Guid customerId = Guid.NewGuid();
        Guid storeId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = now });
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Mgr", PhoneNumber = "+962700800030", PasswordHash = "h", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = managerId, Name = "Mgr", Role = "manager", MallID = mallId });
        db.Management.Add(new Management { ManagerId = managerId, StoreId = storeId, CreatedAt = now });
        db.UserProfiles.Add(new UserProfile { Id = customerId, Name = "Customer", PhoneNumber = "+962700800031", PasswordHash = "h", Role = "user", MallID = mallId });
        db.Stores.Add(new Store { Id = storeId, Name = "Store", MallID = mallId });
        db.Transactions.Add(new Transaction { Id = 501, UserId = customerId, StoreId = storeId, ReceiptId = "SR1", Price = 50, Points = 5000, CreatedAt = now, TransactionStatus = "completed" });
        await db.SaveChangesAsync();

        var access = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        var svc = new RewardsService(db, new PhoneNumberService(), new NoOpUserPointsUpdatesService(), access);

        var result = await svc.GetReceiptDetailsForUserAsync(managerId, 501);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetReceiptDetailsForUserAsync_ThrowsForbidden_WhenNeitherOwnerNorManager()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        Guid ownerId = Guid.NewGuid();
        Guid storeId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = now });
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700800040", PasswordHash = "h", Role = "user", MallID = mallId });
        db.UserProfiles.Add(new UserProfile { Id = ownerId, Name = "Owner", PhoneNumber = "+962700800041", PasswordHash = "h", Role = "user", MallID = mallId });
        db.Stores.Add(new Store { Id = storeId, Name = "Store", MallID = mallId });
        db.Transactions.Add(new Transaction { Id = 502, UserId = ownerId, StoreId = storeId, ReceiptId = "FR1", Price = 20, Points = 2000, CreatedAt = now, TransactionStatus = "completed" });
        await db.SaveChangesAsync();

        var access = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        var svc = new RewardsService(db, new PhoneNumberService(), new NoOpUserPointsUpdatesService(), access);

        await Assert.ThrowsAsync<ApiForbiddenException>(() =>
            svc.GetReceiptDetailsForUserAsync(userId, 502));
    }

    [Fact]
    public async Task GetReceiptDetailsForUserAsync_ReturnsNull_WhenTransactionNotFound()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = DateTimeOffset.UtcNow });
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700800050", PasswordHash = "h", Role = "user", MallID = mallId });
        await db.SaveChangesAsync();

        var access = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        var svc = new RewardsService(db, new PhoneNumberService(), new NoOpUserPointsUpdatesService(), access);

        var result = await svc.GetReceiptDetailsForUserAsync(userId, 9999);
        Assert.Null(result);
    }

    // ── RewardsService.RedeemCouponBySerialAsync — inactive/expired via serial ─

    [Fact]
    public async Task RedeemCouponBySerialAsync_InactiveCoupon_Throws()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        Guid couponId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = now });
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700800060", PasswordHash = "h", Role = "user", MallID = mallId });
        db.Coupons.Add(new Coupon { Id = couponId, Type = "discount", Discription = "X", StartAt = now.AddDays(-1), EndAt = now.AddDays(5), IsActive = false, MallID = mallId, CreatedAt = now });
        db.UserCoupons.Add(new UserCoupon { SerialNumber = "44444444", UserId = userId, CouponId = couponId, IsRedeemed = false, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var access = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        var svc = new RewardsService(db, new PhoneNumberService(), new NoOpUserPointsUpdatesService(), access);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RedeemCouponBySerialAsync("44444444"));
        Assert.Contains("not active", ex.Message);
    }

    [Fact]
    public async Task RedeemCouponBySerialAsync_ExpiredCoupon_Throws()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        Guid couponId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = now });
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700800061", PasswordHash = "h", Role = "user", MallID = mallId });
        db.Coupons.Add(new Coupon { Id = couponId, Type = "discount", Discription = "X", StartAt = now.AddDays(-10), EndAt = now.AddDays(-1), IsActive = true, MallID = mallId, CreatedAt = now.AddDays(-10) });
        db.UserCoupons.Add(new UserCoupon { SerialNumber = "55555555", UserId = userId, CouponId = couponId, IsRedeemed = false, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var access = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        var svc = new RewardsService(db, new PhoneNumberService(), new NoOpUserPointsUpdatesService(), access);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RedeemCouponBySerialAsync("55555555"));
        Assert.Contains("outside redeem period", ex.Message);
    }

    // ── AnnouncementsService.ValidateAnnouncementRequestAsync — store branches ─

    [Fact]
    public async Task CreateAnnouncementAsync_StoreInDifferentMall_ThrowsForbidden()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid otherMallId = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();
        Guid otherStoreId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall A", CreatedAt = now });
        db.Malls.Add(new Mall { Id = otherMallId, Name = "Mall B", CreatedAt = now });
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Mgr", PhoneNumber = "+962700800070", PasswordHash = "h", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = managerId, Name = "Mgr", Role = "manager", MallID = mallId });
        db.Stores.Add(new Store { Id = otherStoreId, Name = "Other Store", MallID = otherMallId });
        await db.SaveChangesAsync();

        var access = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        var svc = new AnnouncementsService(db, access, NullLogger<AnnouncementsService>.Instance);

        var request = new CreateAnnouncementRequest
        {
            Title = "Test",
            Content = "...",
            StoreId = otherStoreId,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(5)
        };

        await Assert.ThrowsAsync<ApiForbiddenException>(() =>
            svc.CreateAnnouncementAsync(managerId, request));
    }

    [Fact]
    public async Task CreateAnnouncementAsync_StoreScopedManager_NotAssignedToStore_ThrowsForbidden()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();
        Guid myStoreId = Guid.NewGuid();
        Guid otherStoreId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = now });
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Mgr", PhoneNumber = "+962700800071", PasswordHash = "h", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = managerId, Name = "Mgr", Role = "manager", MallID = mallId });
        db.Stores.AddRange(
            new Store { Id = myStoreId, Name = "My Store", MallID = mallId },
            new Store { Id = otherStoreId, Name = "Other Store", MallID = mallId });
        db.Management.Add(new Management { ManagerId = managerId, StoreId = myStoreId, CreatedAt = now });
        await db.SaveChangesAsync();

        var access = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        var svc = new AnnouncementsService(db, access, NullLogger<AnnouncementsService>.Instance);

        var request = new CreateAnnouncementRequest
        {
            Title = "Test",
            Content = "...",
            StoreId = otherStoreId,  // not assigned
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(5)
        };

        await Assert.ThrowsAsync<ApiForbiddenException>(() =>
            svc.CreateAnnouncementAsync(managerId, request));
    }

    [Fact]
    public async Task CreateAnnouncementAsync_InvalidStore_ThrowsValidation()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = now });
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Mgr", PhoneNumber = "+962700800072", PasswordHash = "h", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = managerId, Name = "Mgr", Role = "manager", MallID = mallId });
        await db.SaveChangesAsync();

        var access = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        var svc = new AnnouncementsService(db, access, NullLogger<AnnouncementsService>.Instance);

        var request = new CreateAnnouncementRequest
        {
            Title = "Test",
            Content = "...",
            StoreId = Guid.NewGuid(),  // non-existent store
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(5)
        };

        await Assert.ThrowsAsync<ApiValidationException>(() =>
            svc.CreateAnnouncementAsync(managerId, request));
    }

    // ── StoresService.ValidateCategoryIdsAsync — invalid IDs branch ──────────

    [Fact]
    public async Task CreateStoreAsync_InvalidCategoryIds_ThrowsValidation()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = now });
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Mgr", PhoneNumber = "+962700800080", PasswordHash = "h", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = managerId, Name = "Mgr", Role = "manager", MallID = mallId });
        await db.SaveChangesAsync();

        var access = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        var svc = new StoresService(db, access, NullLogger<StoresService>.Instance);

        var request = new DTOs.Stores.CreateStoreRequest
        {
            Name = "Test Store",
            CategoryIds = new List<long> { 999L }  // non-existent category
        };

        await Assert.ThrowsAsync<ApiValidationException>(() =>
            svc.CreateStoreAsync(managerId, request));
    }

    // ── UserAccessService — manager.MallID != user.MallID (log warning path) ─

    [Fact]
    public async Task GetUserAccessContextAsync_LogsWarning_WhenManagerMallDiffersFromUserMall()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallA = Guid.NewGuid();
        Guid mallB = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();

        db.Malls.Add(new Mall { Id = mallA, Name = "Mall A", CreatedAt = DateTimeOffset.UtcNow });
        db.Malls.Add(new Mall { Id = mallB, Name = "Mall B", CreatedAt = DateTimeOffset.UtcNow });
        // UserProfile is in Mall A, Manager entity is in Mall B
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Mgr", PhoneNumber = "+962700800090", PasswordHash = "h", Role = "manager", MallID = mallA });
        db.Managers.Add(new Manager { Id = managerId, Name = "Mgr", Role = "manager", MallID = mallB });
        await db.SaveChangesAsync();

        var svc = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        var context = await svc.GetUserAccessContextAsync(managerId);

        // MallID comes from manager when mismatch — this exercises the LogWarning branch
        Assert.Equal(mallB, context.MallID);
        Assert.True(context.IsManager);
    }

    // ── AuthService — RegisterManagerAsync branches ──────────────────────────

    private static AuthService CreateAuthService(AppDbContext db)
    {
        var session = new SessionService(db);
        return new AuthService(db, new PhoneNumberService(), new PasswordHasher<UserProfile>(), session);
    }

    [Fact]
    public async Task RegisterAsync_ManagerMallMismatch_ThrowsValidation()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallA = Guid.NewGuid();
        Guid mallB = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();

        db.Malls.Add(new Mall { Id = mallA, Name = "Mall A", CreatedAt = DateTimeOffset.UtcNow });
        db.Malls.Add(new Mall { Id = mallB, Name = "Mall B", CreatedAt = DateTimeOffset.UtcNow });
        db.Managers.Add(new Manager { Id = managerId, Name = "Mgr", Role = "manager", MallID = mallA });
        await db.SaveChangesAsync();

        var svc = CreateAuthService(db);
        var dto = new RegisterRequestDto
        {
            ManagerId = managerId,
            MallID = mallB,  // different from manager's mall
            PhoneNumber = "+962700800100",
            Password = "pass123",
            Name = "Mgr"
        };

        await Assert.ThrowsAsync<AuthValidationException>(() => svc.RegisterAsync(dto));
    }

    [Fact]
    public async Task RegisterAsync_ManagerAlreadyRegistered_ThrowsConflict()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = DateTimeOffset.UtcNow });
        db.Managers.Add(new Manager { Id = managerId, Name = "Mgr", Role = "manager", MallID = mallId });
        // Manager already has a UserProfile
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Mgr", PhoneNumber = "+962700800101", PasswordHash = "h", Role = "manager", MallID = mallId });
        await db.SaveChangesAsync();

        var svc = CreateAuthService(db);
        var dto = new RegisterRequestDto
        {
            ManagerId = managerId,
            MallID = mallId,
            PhoneNumber = "+962700800102",
            Password = "pass123",
            Name = "Mgr"
        };

        await Assert.ThrowsAsync<AuthConflictException>(() => svc.RegisterAsync(dto));
    }

    [Fact]
    public async Task RegisterAsync_PhoneAlreadyInSameMall_ForManager_ThrowsConflict()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = DateTimeOffset.UtcNow });
        db.Managers.Add(new Manager { Id = managerId, Name = "Mgr", Role = "manager", MallID = mallId });
        // A different user has the same phone number in the same mall
        db.UserProfiles.Add(new UserProfile { Id = Guid.NewGuid(), Name = "Existing", PhoneNumber = "+962700800103", PasswordHash = "h", Role = "user", MallID = mallId });
        await db.SaveChangesAsync();

        var svc = CreateAuthService(db);
        var dto = new RegisterRequestDto
        {
            ManagerId = managerId,
            MallID = mallId,
            PhoneNumber = "+962700800103",
            Password = "pass123",
            Name = "Mgr"
        };

        await Assert.ThrowsAsync<AuthConflictException>(() => svc.RegisterAsync(dto));
    }

    [Fact]
    public async Task RegisterAsync_PhoneInDifferentMall_ForManager_ThrowsConflict()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallA = Guid.NewGuid();
        Guid mallB = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();

        db.Malls.Add(new Mall { Id = mallA, Name = "Mall A", CreatedAt = DateTimeOffset.UtcNow });
        db.Malls.Add(new Mall { Id = mallB, Name = "Mall B", CreatedAt = DateTimeOffset.UtcNow });
        db.Managers.Add(new Manager { Id = managerId, Name = "Mgr", Role = "manager", MallID = mallA });
        // Same phone in different mall
        db.UserProfiles.Add(new UserProfile { Id = Guid.NewGuid(), Name = "Other", PhoneNumber = "+962700800104", PasswordHash = "h", Role = "user", MallID = mallB });
        await db.SaveChangesAsync();

        var svc = CreateAuthService(db);
        var dto = new RegisterRequestDto
        {
            ManagerId = managerId,
            MallID = mallA,
            PhoneNumber = "+962700800104",
            Password = "pass123",
            Name = "Mgr"
        };

        await Assert.ThrowsAsync<AuthConflictException>(() => svc.RegisterAsync(dto));
    }

    // ── AuthService.ManagerQuickLoginAsync — existing user update path ───────

    [Fact]
    public async Task ManagerQuickLoginAsync_ExistingUserProfile_UpdatesAndReturnsSession()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = DateTimeOffset.UtcNow });
        db.Managers.Add(new Manager { Id = managerId, Name = "Admin", Role = "manager", MallID = mallId });
        // UserProfile already exists (e.g., previously quick-logged)
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "OldName", PhoneNumber = "+962700800110", PasswordHash = "hash", Role = "manager", MallID = mallId });
        await db.SaveChangesAsync();

        var svc = CreateAuthService(db);
        var result = await svc.ManagerQuickLoginAsync(new ManagerQuickLoginRequestDto { ManagerId = managerId });

        Assert.Equal("Admin", result.Name);
        Assert.NotNull(result.SessionId);
    }
}
