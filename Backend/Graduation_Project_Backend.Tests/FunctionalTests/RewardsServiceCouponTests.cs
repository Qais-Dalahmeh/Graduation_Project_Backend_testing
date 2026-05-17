using Graduation_Project_Backend.Models.Entities;
using Graduation_Project_Backend.Models.User;
using Graduation_Project_Backend.Service;
using Graduation_Project_Backend.Service.Common;
using Graduation_Project_Backend.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Graduation_Project_Backend.Tests.FunctionalTests
{
    public sealed class RewardsServiceCouponTests
    {
        private static RewardsService CreateService(AppDbContext db)
        {
            var access = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
            return new RewardsService(db, new PhoneNumberService(), new NoOpUserPointsUpdatesService(), access);
        }

        [Fact]
        public async Task RedeemCouponAsync_ValidCouponWithCostPoints_DeductsFromUserBalance()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();
            Guid couponId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700300001", PasswordHash = "hash", Role = "user", MallID = mallId, TotalPoints = 500 });
            db.Coupons.Add(new Coupon { Id = couponId, Type = "discount", Discription = "10% off", StartAt = now.AddDays(-1), EndAt = now.AddDays(7), IsActive = true, CostPoint = 100, MallID = mallId, CreatedAt = now });
            await db.SaveChangesAsync();

            var userCoupon = await CreateService(db).RedeemCouponAsync(userId, couponId);

            Assert.Equal(userId, userCoupon.UserId);
            Assert.Equal(couponId, userCoupon.CouponId);
            Assert.False(userCoupon.IsRedeemed);
            Assert.Equal(8, userCoupon.SerialNumber.Length);
            Assert.True(userCoupon.SerialNumber.All(char.IsDigit));

            Assert.Equal(400, db.UserProfiles.Single(u => u.Id == userId).TotalPoints);
        }

        [Fact]
        public async Task RedeemCouponAsync_FreeCoupon_DoesNotChangePoints()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();
            Guid couponId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700300002", PasswordHash = "hash", Role = "user", MallID = mallId, TotalPoints = 200 });
            db.Coupons.Add(new Coupon { Id = couponId, Type = "free", Discription = "Free gift", StartAt = now.AddDays(-1), EndAt = now.AddDays(7), IsActive = true, CostPoint = null, MallID = mallId, CreatedAt = now });
            await db.SaveChangesAsync();

            await CreateService(db).RedeemCouponAsync(userId, couponId);

            Assert.Equal(200, db.UserProfiles.Single(u => u.Id == userId).TotalPoints);
        }

        [Fact]
        public async Task RedeemCouponAsync_NotEnoughPoints_Throws()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();
            Guid couponId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700300003", PasswordHash = "hash", Role = "user", MallID = mallId, TotalPoints = 50 });
            db.Coupons.Add(new Coupon { Id = couponId, Type = "discount", Discription = "Big discount", StartAt = now.AddDays(-1), EndAt = now.AddDays(7), IsActive = true, CostPoint = 200, MallID = mallId, CreatedAt = now });
            await db.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService(db).RedeemCouponAsync(userId, couponId));

            Assert.Contains("enough points", ex.Message);
        }

        [Fact]
        public async Task RedeemCouponAsync_InactiveCoupon_Throws()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();
            Guid couponId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700300004", PasswordHash = "hash", Role = "user", MallID = mallId, TotalPoints = 500 });
            db.Coupons.Add(new Coupon { Id = couponId, Type = "discount", Discription = "Inactive", StartAt = now.AddDays(-1), EndAt = now.AddDays(7), IsActive = false, CostPoint = null, MallID = mallId, CreatedAt = now });
            await db.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService(db).RedeemCouponAsync(userId, couponId));

            Assert.Contains("not active", ex.Message);
        }

        [Fact]
        public async Task RedeemCouponAsync_ExpiredCoupon_Throws()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();
            Guid couponId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700300005", PasswordHash = "hash", Role = "user", MallID = mallId, TotalPoints = 500 });
            db.Coupons.Add(new Coupon { Id = couponId, Type = "discount", Discription = "Expired", StartAt = now.AddDays(-10), EndAt = now.AddDays(-1), IsActive = true, CostPoint = null, MallID = mallId, CreatedAt = now.AddDays(-10) });
            await db.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService(db).RedeemCouponAsync(userId, couponId));

            Assert.Contains("outside redeem period", ex.Message);
        }

        [Fact]
        public async Task RedeemCouponAsync_FutureCoupon_Throws()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();
            Guid couponId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700300006", PasswordHash = "hash", Role = "user", MallID = mallId, TotalPoints = 500 });
            db.Coupons.Add(new Coupon { Id = couponId, Type = "discount", Discription = "Not yet", StartAt = now.AddDays(1), EndAt = now.AddDays(7), IsActive = true, CostPoint = null, MallID = mallId, CreatedAt = now });
            await db.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService(db).RedeemCouponAsync(userId, couponId));

            Assert.Contains("outside redeem period", ex.Message);
        }

        [Fact]
        public async Task RedeemCouponBySerialAsync_ValidSerial_MarksAsRedeemed()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();
            Guid couponId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700300007", PasswordHash = "hash", Role = "user", MallID = mallId, TotalPoints = 0 });
            db.Coupons.Add(new Coupon { Id = couponId, Type = "discount", Discription = "10% off", StartAt = now.AddDays(-1), EndAt = now.AddDays(7), IsActive = true, CostPoint = null, MallID = mallId, CreatedAt = now });
            db.UserCoupons.Add(new UserCoupon { SerialNumber = "12345678", UserId = userId, CouponId = couponId, IsRedeemed = false, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();

            var result = await CreateService(db).RedeemCouponBySerialAsync("12345678");

            Assert.True(result.IsRedeemed);
            Assert.True(db.UserCoupons.Single().IsRedeemed);
        }

        [Fact]
        public async Task RedeemCouponBySerialAsync_AlreadyRedeemed_Throws()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();
            Guid couponId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700300008", PasswordHash = "hash", Role = "user", MallID = mallId, TotalPoints = 0 });
            db.Coupons.Add(new Coupon { Id = couponId, Type = "discount", Discription = "10% off", StartAt = now.AddDays(-1), EndAt = now.AddDays(7), IsActive = true, CostPoint = null, MallID = mallId, CreatedAt = now });
            db.UserCoupons.Add(new UserCoupon { SerialNumber = "87654321", UserId = userId, CouponId = couponId, IsRedeemed = true, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService(db).RedeemCouponBySerialAsync("87654321"));

            Assert.Contains("already redeemed", ex.Message);
        }

        [Fact]
        public async Task RedeemCouponBySerialAsync_NonExistentSerial_Throws()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService(db).RedeemCouponBySerialAsync("00000000"));

            Assert.Contains("serial not found", ex.Message.ToLower());
        }

        [Fact]
        public async Task ProcessTransactionAsync_ValidPurchase_AwardsPointsAndRecordsTransaction()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            Guid storeId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.Stores.Add(new Store { Id = storeId, Name = "Nike", MallID = mallId });
            db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700300009", PasswordHash = "hash", Role = "user", MallID = mallId, TotalPoints = 0 });
            await db.SaveChangesAsync();

            var result = await CreateService(db).ProcessTransactionAsync(
                phoneNumber: "+962700300009",
                storeId: storeId,
                receiptId: "receipt-001",
                receiptDescription: "Purchase at Nike",
                price: 50.00m);

            Assert.Equal(5000, result.Points);          // 50 * 100
            Assert.Equal(5000, result.NewTotalPoints);
            Assert.Equal("receipt-001", result.ReceiptId);
            Assert.Single(db.Transactions);
            Assert.Equal(5000, db.UserProfiles.Single(u => u.Id == userId).TotalPoints);
        }

        [Fact]
        public async Task ProcessTransactionAsync_DuplicateReceiptId_Throws()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            Guid storeId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.Stores.Add(new Store { Id = storeId, Name = "Nike", MallID = mallId });
            db.UserProfiles.Add(new UserProfile { Id = Guid.NewGuid(), Name = "User", PhoneNumber = "+962700300010", PasswordHash = "hash", Role = "user", MallID = mallId, TotalPoints = 0 });
            db.Transactions.Add(new Transaction { Id = 1, UserId = Guid.NewGuid(), StoreId = storeId, ReceiptId = "dup-receipt", Price = 10, Points = 1000, TransactionStatus = "completed", CreatedAt = now });
            await db.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService(db).ProcessTransactionAsync("+962700300010", storeId, "dup-receipt", null, 20.00m));

            Assert.Contains("already exists", ex.Message);
        }
    }
}

