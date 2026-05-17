using Graduation_Project_Backend.DTOs.Receipts;
using Graduation_Project_Backend.Models.Entities;
using Graduation_Project_Backend.Models.User;
using Graduation_Project_Backend.Service;
using Graduation_Project_Backend.Service.Common;
using Graduation_Project_Backend.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Graduation_Project_Backend.Tests.BranchCoverageTests;

/// <summary>
/// Coverage-gap tests for RewardsService methods not yet reached by other test suites.
/// Covers: GetTransactionDetailsAsync, GetCouponsAsync, GetCouponDetailsAsync,
///         GetUserCouponsViewAsync, GetUserTotalPointsAsync, GetMyReceiptsAsync.
/// </summary>
public sealed class CoverageGapTests_RewardsService
{
    private static RewardsService CreateService(AppDbContext db)
    {
        var access = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        return new RewardsService(db, new PhoneNumberService(), new NoOpUserPointsUpdatesService(), access);
    }

    // â”€â”€ GetTransactionDetailsAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task GetTransactionDetailsAsync_ReturnsNull_WhenTransactionNotFound()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        var result = await CreateService(db).GetTransactionDetailsAsync(999);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetTransactionDetailsAsync_ReturnsDetails_WhenFound()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid storeId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Test Mall", CreatedAt = now });
        db.Stores.Add(new Store { Id = storeId, Name = "Test Store", MallID = mallId });
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "Ali", PhoneNumber = "+962700400001", PasswordHash = "h", Role = "user", MallID = mallId });
        db.Transactions.Add(new Transaction { Id = 1, UserId = userId, StoreId = storeId, ReceiptId = "R1", Price = 100, Points = 10000, CreatedAt = now, TransactionStatus = "completed" });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetTransactionDetailsAsync(1);

        Assert.NotNull(result);
    }

    // â”€â”€ GetCouponsAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task GetCouponsAsync_ReturnsAll_WhenNoFilter()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = now });
        db.Coupons.AddRange(
            new Coupon { Id = Guid.NewGuid(), Type = "discount", Discription = "A", StartAt = now.AddDays(-1), EndAt = now.AddDays(1), IsActive = true, MallID = mallId, CreatedAt = now },
            new Coupon { Id = Guid.NewGuid(), Type = "gift", Discription = "B", StartAt = now.AddDays(-10), EndAt = now.AddDays(-5), IsActive = false, MallID = mallId, CreatedAt = now.AddDays(-10) });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetCouponsAsync(null);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetCouponsAsync_ReturnsOnlyActive_WhenFilterIsTrue()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = now });
        db.Coupons.AddRange(
            new Coupon { Id = Guid.NewGuid(), Type = "discount", Discription = "Active", StartAt = now.AddDays(-1), EndAt = now.AddDays(1), IsActive = true, MallID = mallId, CreatedAt = now },
            new Coupon { Id = Guid.NewGuid(), Type = "gift", Discription = "Inactive", StartAt = now.AddDays(-10), EndAt = now.AddDays(-5), IsActive = false, MallID = mallId, CreatedAt = now.AddDays(-10) });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetCouponsAsync(true);

        Assert.Single(result);
        Assert.True(result[0].IsActive);
    }

    [Fact]
    public async Task GetCouponsAsync_ReturnsEmpty_WhenActiveFilterButNoneActive()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        var result = await CreateService(db).GetCouponsAsync(false);
        Assert.Empty(result);
    }

    // â”€â”€ GetCouponDetailsAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task GetCouponDetailsAsync_ReturnsNull_WhenNotFound()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        var result = await CreateService(db).GetCouponDetailsAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCouponDetailsAsync_ReturnsDetails_WhenFound()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid couponId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = now });
        db.Coupons.Add(new Coupon { Id = couponId, Type = "discount", Discription = "10% off", StartAt = now.AddDays(-1), EndAt = now.AddDays(7), IsActive = true, CostPoint = 50, MallID = mallId, CreatedAt = now });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetCouponDetailsAsync(couponId);

        Assert.NotNull(result);
    }

    // â”€â”€ GetUserCouponsViewAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task GetUserCouponsViewAsync_ReturnsEmpty_WhenNoUserCoupons()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        var result = await CreateService(db).GetUserCouponsViewAsync(Guid.NewGuid());
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUserCouponsViewAsync_ReturnsList_WhenUserHasCoupons()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        Guid couponId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = now });
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700400010", PasswordHash = "h", Role = "user", MallID = mallId });
        db.Coupons.Add(new Coupon { Id = couponId, Type = "discount", Discription = "5% off", StartAt = now.AddDays(-1), EndAt = now.AddDays(5), IsActive = true, MallID = mallId, CreatedAt = now });
        db.UserCoupons.Add(new UserCoupon { SerialNumber = "11111111", UserId = userId, CouponId = couponId, IsRedeemed = false, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetUserCouponsViewAsync(userId);

        Assert.Single(result);
    }

    // â”€â”€ GetUserTotalPointsAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task GetUserTotalPointsAsync_ReturnsNull_WhenUserNotFound()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        var result = await CreateService(db).GetUserTotalPointsAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserTotalPointsAsync_ReturnsPoints_WhenUserFound()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = now });
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700400020", PasswordHash = "h", Role = "user", MallID = mallId, TotalPoints = 750 });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetUserTotalPointsAsync(userId);

        Assert.Equal(750, result);
    }

    // â”€â”€ GetMyReceiptsAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task GetMyReceiptsAsync_ReturnsPagedReceipts_WithStoreFilter()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid storeId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = now });
        db.Stores.Add(new Store { Id = storeId, Name = "Shop", MallID = mallId });
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700400030", PasswordHash = "h", Role = "user", MallID = mallId });
        db.Transactions.AddRange(
            new Transaction { Id = 10, UserId = userId, StoreId = storeId, ReceiptId = "R10", Price = 20, Points = 2000, CreatedAt = now, TransactionStatus = "completed" },
            new Transaction { Id = 11, UserId = userId, StoreId = Guid.NewGuid(), ReceiptId = "R11", Price = 30, Points = 3000, CreatedAt = now, TransactionStatus = "completed" });
        await db.SaveChangesAsync();

        var userAccessService = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        var service = new RewardsService(db, new PhoneNumberService(), new NoOpUserPointsUpdatesService(), userAccessService);

        var query = new ReceiptListQuery { Page = 1, PageSize = 10, StoreId = storeId };
        var result = await service.GetMyReceiptsAsync(userId, query);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetMyReceiptsAsync_AppliesDateAndStatusFilters()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid storeId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = now });
        db.Stores.Add(new Store { Id = storeId, Name = "Shop", MallID = mallId });
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700400031", PasswordHash = "h", Role = "user", MallID = mallId });
        db.Transactions.AddRange(
            new Transaction { Id = 20, UserId = userId, StoreId = storeId, ReceiptId = "R20", Price = 10, Points = 1000, CreatedAt = now.AddDays(-5), TransactionStatus = "completed" },
            new Transaction { Id = 21, UserId = userId, StoreId = storeId, ReceiptId = "R21", Price = 10, Points = 1000, CreatedAt = now, TransactionStatus = "pending" });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Status filter
        var statusQuery = new ReceiptListQuery { Page = 1, PageSize = 10, Status = "completed" };
        var byStatus = await service.GetMyReceiptsAsync(userId, statusQuery);
        Assert.Equal(1, byStatus.TotalCount);

        // Date range filter
        var dateQuery = new ReceiptListQuery { Page = 1, PageSize = 10, From = now.AddDays(-7), To = now.AddDays(-3) };
        var byDate = await service.GetMyReceiptsAsync(userId, dateQuery);
        Assert.Equal(1, byDate.TotalCount);
    }

    [Fact]
    public async Task GetMyReceiptsAsync_InvalidDateRange_ThrowsValidation()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid userId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        var service = CreateService(db);
        var query = new ReceiptListQuery { Page = 1, PageSize = 10, From = now, To = now.AddDays(-1) };

        await Assert.ThrowsAsync<ApiValidationException>(() =>
            service.GetMyReceiptsAsync(userId, query));
    }
}

