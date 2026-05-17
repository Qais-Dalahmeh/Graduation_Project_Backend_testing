using Graduation_Project_Backend.DTOs.Dashboard;
using Graduation_Project_Backend.Models.Entities;
using Graduation_Project_Backend.Models.User;
using Graduation_Project_Backend.Service;
using Graduation_Project_Backend.Service.Common;
using Graduation_Project_Backend.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Graduation_Project_Backend.Tests.BranchCoverageTests;

/// <summary>
/// Coverage-gap tests for DashboardService methods not yet reached.
/// Covers: GetSalesAsync, GetPointsAsync, GetCouponsAsync, GetActivityAsync.
/// </summary>
public sealed class CoverageGapTests_DashboardService
{
    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Creates a mall-wide manager (no Management rows = mall-wide scope).</summary>
    private static (AppDbContext db, Guid managerId, Guid mallId, Guid storeId) SetupMallWideManager()
    {
        AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();
        Guid storeId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Grand Mall", CreatedAt = now });
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Boss", PhoneNumber = "+962700500001", PasswordHash = "h", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = managerId, Name = "Boss", Role = "manager", MallID = mallId });
        db.Stores.Add(new Store { Id = storeId, Name = "Nike", MallID = mallId });
        db.SaveChanges();

        return (db, managerId, mallId, storeId);
    }

    /// <summary>Creates a store-scoped manager assigned to one store.</summary>
    private static (AppDbContext db, Guid managerId, Guid mallId, Guid storeId) SetupStoreScopedManager()
    {
        AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();
        Guid storeId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Grand Mall", CreatedAt = now });
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Store Mgr", PhoneNumber = "+962700500002", PasswordHash = "h", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = managerId, Name = "Store Mgr", Role = "manager", MallID = mallId });
        db.Stores.Add(new Store { Id = storeId, Name = "Adidas", MallID = mallId });
        db.Management.Add(new Management { ManagerId = managerId, StoreId = storeId, CreatedAt = now });
        db.SaveChanges();

        return (db, managerId, mallId, storeId);
    }

    private static DashboardService CreateService(AppDbContext db)
    {
        var access = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        return new DashboardService(db, access);
    }

    // â”€â”€ GetSalesAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task GetSalesAsync_MallWideManager_ReturnsAggregatedSales()
    {
        var (db, managerId, mallId, storeId) = SetupMallWideManager();
        using (db)
        {
            Guid userId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.UserProfiles.Add(new UserProfile { Id = userId, Name = "Buyer", PhoneNumber = "+962700500010", PasswordHash = "h", Role = "user", MallID = mallId });
            db.Transactions.AddRange(
                new Transaction { Id = 100, UserId = userId, StoreId = storeId, ReceiptId = "s1", Price = 50, Points = 5000, CreatedAt = now.AddDays(-1), TransactionStatus = "completed" },
                new Transaction { Id = 101, UserId = userId, StoreId = storeId, ReceiptId = "s2", Price = 30, Points = 3000, CreatedAt = now, TransactionStatus = "completed" });
            await db.SaveChangesAsync();

            var service = CreateService(db);
            DashboardSalesResponse result = await service.GetSalesAsync(managerId, new DashboardDateRangeQuery());

            Assert.Equal(80m, result.TotalSalesAmount);
            Assert.Equal(2, result.TotalTransactions);
            Assert.NotEmpty(result.DailySales);
            Assert.NotEmpty(result.TopStores);
        }
    }

    [Fact]
    public async Task GetSalesAsync_NonManager_ThrowsForbidden()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = DateTimeOffset.UtcNow });
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700500011", PasswordHash = "h", Role = "user", MallID = mallId });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await Assert.ThrowsAsync<ApiForbiddenException>(() =>
            service.GetSalesAsync(userId, new DashboardDateRangeQuery()));
    }

    [Fact]
    public async Task GetSalesAsync_InvalidDateRange_ThrowsValidation()
    {
        var (db, managerId, _, _) = SetupMallWideManager();
        using (db)
        {
            var service = CreateService(db);
            var query = new DashboardDateRangeQuery { From = DateTimeOffset.UtcNow, To = DateTimeOffset.UtcNow.AddDays(-1) };

            await Assert.ThrowsAsync<ApiValidationException>(() =>
                service.GetSalesAsync(managerId, query));
        }
    }

    // â”€â”€ GetPointsAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task GetPointsAsync_MallWideManager_ReturnsPointsSummary()
    {
        var (db, managerId, mallId, storeId) = SetupMallWideManager();
        using (db)
        {
            Guid userId = Guid.NewGuid();
            Guid couponId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.UserProfiles.Add(new UserProfile { Id = userId, Name = "Buyer", PhoneNumber = "+962700500020", PasswordHash = "h", Role = "user", MallID = mallId });
            db.Coupons.Add(new Coupon { Id = couponId, Type = "discount", Discription = "5%", StartAt = now.AddDays(-5), EndAt = now.AddDays(5), IsActive = true, CostPoint = 100, MallID = mallId, CreatedAt = now });
            db.Transactions.Add(new Transaction { Id = 200, UserId = userId, StoreId = storeId, ReceiptId = "p1", Price = 40, Points = 4000, CreatedAt = now, TransactionStatus = "completed" });
            db.UserCoupons.Add(new UserCoupon { SerialNumber = "22222222", UserId = userId, CouponId = couponId, IsRedeemed = true, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();

            var service = CreateService(db);
            DashboardPointsResponse result = await service.GetPointsAsync(managerId, new DashboardDateRangeQuery());

            Assert.Equal(4000, result.TotalPointsIssued);
            Assert.NotNull(result.TotalPointsRedeemed);
        }
    }

    [Fact]
    public async Task GetPointsAsync_StoreScopedManager_ReturnsNullRedeemed()
    {
        var (db, managerId, _, _) = SetupStoreScopedManager();
        using (db)
        {
            var service = CreateService(db);
            DashboardPointsResponse result = await service.GetPointsAsync(managerId, new DashboardDateRangeQuery());

            // Store-scoped managers don't get coupon data
            Assert.Null(result.TotalPointsRedeemed);
            Assert.Empty(result.DailyRedeemed);
        }
    }

    // â”€â”€ GetCouponsAsync (DashboardService) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task DashboardGetCouponsAsync_MallWideManager_ReturnsCouponStats()
    {
        var (db, managerId, mallId, _) = SetupMallWideManager();
        using (db)
        {
            Guid userId = Guid.NewGuid();
            Guid couponId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700500030", PasswordHash = "h", Role = "user", MallID = mallId });
            db.Coupons.Add(new Coupon { Id = couponId, Type = "discount", Discription = "X", StartAt = now.AddDays(-1), EndAt = now.AddDays(5), IsActive = true, CostPoint = 50, MallID = mallId, CreatedAt = now });
            db.UserCoupons.AddRange(
                new UserCoupon { SerialNumber = "33333331", UserId = userId, CouponId = couponId, IsRedeemed = true, CreatedAt = DateTime.UtcNow },
                new UserCoupon { SerialNumber = "33333332", UserId = userId, CouponId = couponId, IsRedeemed = false, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();

            var service = CreateService(db);
            DashboardCouponsResponse result = await service.GetCouponsAsync(managerId, new DashboardDateRangeQuery());

            Assert.False(result.IsScopeLimited);
            Assert.Equal(1, result.TotalActiveCoupons);
            Assert.Equal(2, result.TotalActivatedUserCoupons);
            Assert.Equal(1, result.TotalRedeemedCoupons);
            Assert.True(result.RedemptionRate > 0);
        }
    }

    [Fact]
    public async Task DashboardGetCouponsAsync_StoreScopedManager_ReturnsLimitedFlag()
    {
        var (db, managerId, _, _) = SetupStoreScopedManager();
        using (db)
        {
            var service = CreateService(db);
            DashboardCouponsResponse result = await service.GetCouponsAsync(managerId, new DashboardDateRangeQuery());

            Assert.True(result.IsScopeLimited);
        }
    }

    [Fact]
    public async Task DashboardGetCouponsAsync_ZeroActivated_RedemptionRateIsZero()
    {
        var (db, managerId, _, _) = SetupMallWideManager();
        using (db)
        {
            var service = CreateService(db);
            DashboardCouponsResponse result = await service.GetCouponsAsync(managerId, new DashboardDateRangeQuery());

            Assert.False(result.IsScopeLimited);
            Assert.Equal(0, result.RedemptionRate);
        }
    }

    // â”€â”€ GetActivityAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task GetActivityAsync_MallWideManager_ReturnsFullActivity()
    {
        var (db, managerId, mallId, storeId) = SetupMallWideManager();
        using (db)
        {
            Guid userId = Guid.NewGuid();
            long catId = 1;
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700500040", PasswordHash = "h", Role = "user", MallID = mallId });
            db.Categories.Add(new Category { Id = catId, Name = "Fashion", MallID = mallId });
            db.StoreCategories.Add(new StoreCategory { StoreId = storeId, CategoryId = catId });
            db.Offers.Add(new Offer { Id = 1L, MallID = mallId, StoreId = storeId, Title = "Sale", Description = "50% off", StartAt = now.AddDays(-1), EndAt = now.AddDays(5), IsActive = true });
            db.Announcements.Add(new Announcement { Id = Guid.NewGuid(), MallID = mallId, Title = "News", Content = "...", AnnouncementType = "general", Priority = "normal", IsActive = true, StartDate = now.AddDays(-1), EndDate = now.AddDays(5), CreatedAt = now, UpdatedAt = now });
            db.Transactions.Add(new Transaction { Id = 300, UserId = userId, StoreId = storeId, ReceiptId = "a1", Price = 25, Points = 2500, CreatedAt = now, TransactionStatus = "completed" });
            await db.SaveChangesAsync();

            var service = CreateService(db);
            DashboardActivityResponse result = await service.GetActivityAsync(managerId, new DashboardDateRangeQuery());

            Assert.Equal(1, result.TotalOffers);
            Assert.Equal(1, result.TotalAnnouncements);
            Assert.Equal(1, result.ActiveOffers);
            Assert.Equal(1, result.ActiveAnnouncements);
            Assert.NotNull(result.UnreadNotifications);
            Assert.NotEmpty(result.CategoryDistribution);
        }
    }

    [Fact]
    public async Task GetActivityAsync_StoreScopedManager_FiltersByAssignedStore()
    {
        var (db, managerId, mallId, storeId) = SetupStoreScopedManager();
        using (db)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;

            // Offer scoped to the assigned store
            db.Offers.Add(new Offer { Id = 2L, MallID = mallId, StoreId = storeId, Title = "Store Sale", Description = "20% off", StartAt = now.AddDays(-1), EndAt = now.AddDays(5), IsActive = true });
            // Offer scoped to a different store (should not appear)
            db.Offers.Add(new Offer { Id = 3L, MallID = mallId, StoreId = Guid.NewGuid(), Title = "Other Sale", Description = "10% off", StartAt = now.AddDays(-1), EndAt = now.AddDays(5), IsActive = true });
            await db.SaveChangesAsync();

            var service = CreateService(db);
            DashboardActivityResponse result = await service.GetActivityAsync(managerId, new DashboardDateRangeQuery());

            Assert.Equal(1, result.TotalOffers);
            Assert.Null(result.UnreadNotifications);  // store-scoped managers don't see unread notifications
        }
    }
}

