using Graduation_Project_Backend.Data;
using Graduation_Project_Backend.DTOs.Offers;
using Graduation_Project_Backend.Models.Entities;
using Graduation_Project_Backend.Models.User;
using Graduation_Project_Backend.Service;
using Graduation_Project_Backend.Service.Common;
using Graduation_Project_Backend.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Graduation_Project_Backend.Tests.QualityTests
{
    public sealed class OffersServiceTests
    {
        private static OffersService CreateService(AppDbContext db)
        {
            var access = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
            return new OffersService(db, access, NullLogger<OffersService>.Instance);
        }

        [Fact]
        public async Task GetVisibleOffersAsync_ReturnsOnlyActiveOffersWithinTimeWindow()
        {
            using AppDbContext db = DbFactory.CreateInMemoryDb();
            Guid mallId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();
            Guid storeId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700100001", PasswordHash = "hash", Role = "user", MallID = mallId });
            db.Stores.Add(new Store { Id = storeId, Name = "Store A", MallID = mallId });
            db.Offers.AddRange(
                new Offer { StoreId = storeId, MallID = mallId, Title = "Valid Offer",    StartAt = now.AddDays(-1), EndAt = now.AddDays(1),  IsActive = true,  MadeAt = now },
                new Offer { StoreId = storeId, MallID = mallId, Title = "Expired Offer",  StartAt = now.AddDays(-3), EndAt = now.AddDays(-1), IsActive = true,  MadeAt = now },
                new Offer { StoreId = storeId, MallID = mallId, Title = "Future Offer",   StartAt = now.AddDays(1),  EndAt = now.AddDays(3),  IsActive = true,  MadeAt = now },
                new Offer { StoreId = storeId, MallID = mallId, Title = "Inactive Offer", StartAt = now.AddDays(-1), EndAt = now.AddDays(1),  IsActive = false, MadeAt = now });
            await db.SaveChangesAsync();

            var result = await CreateService(db).GetVisibleOffersAsync(userId);

            Assert.Single(result);
            Assert.Equal("Valid Offer", result[0].Title);
        }

        [Fact]
        public async Task GetVisibleOffersAsync_ExcludesOffersFromOtherMalls()
        {
            using AppDbContext db = DbFactory.CreateInMemoryDb();
            Guid mallA = Guid.NewGuid();
            Guid mallB = Guid.NewGuid();
            Guid userId = Guid.NewGuid();
            Guid storeA = Guid.NewGuid();
            Guid storeB = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.AddRange(
                new Mall { Id = mallA, Name = "Mall A", CreatedAt = now },
                new Mall { Id = mallB, Name = "Mall B", CreatedAt = now });
            db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700100002", PasswordHash = "hash", Role = "user", MallID = mallA });
            db.Stores.AddRange(
                new Store { Id = storeA, Name = "Store A", MallID = mallA },
                new Store { Id = storeB, Name = "Store B", MallID = mallB });
            db.Offers.AddRange(
                new Offer { StoreId = storeA, MallID = mallA, Title = "Mall A Offer", StartAt = now.AddDays(-1), EndAt = now.AddDays(1), IsActive = true, MadeAt = now },
                new Offer { StoreId = storeB, MallID = mallB, Title = "Mall B Offer", StartAt = now.AddDays(-1), EndAt = now.AddDays(1), IsActive = true, MadeAt = now });
            await db.SaveChangesAsync();

            var result = await CreateService(db).GetVisibleOffersAsync(userId);

            Assert.Single(result);
            Assert.Equal("Mall A Offer", result[0].Title);
        }

        [Fact]
        public async Task CreateOfferAsync_MallWideManager_CreatesOfferSuccessfully()
        {
            using AppDbContext db = DbFactory.CreateInMemoryDb();
            Guid mallId = Guid.NewGuid();
            Guid managerId = Guid.NewGuid();
            Guid storeId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Manager", PhoneNumber = "+962700100003", PasswordHash = "hash", Role = "manager", MallID = mallId });
            db.Managers.Add(new Manager { Id = managerId, Name = "Manager", Role = "manager", MallID = mallId });
            db.Stores.Add(new Store { Id = storeId, Name = "Nike", MallID = mallId });
            await db.SaveChangesAsync();

            var response = await CreateService(db).CreateOfferAsync(managerId, new CreateOfferRequest
            {
                StoreId = storeId,
                Title = "  Summer Sale  ",
                Description = "20% off everything",
                StartAt = now.AddDays(-1),
                EndAt = now.AddDays(7),
                IsActive = true
            });

            Assert.Equal("Summer Sale", response.Title);
            Assert.Equal(mallId, response.MallID);
            Assert.Equal(storeId, response.StoreId);
            Assert.Single(db.Offers);
        }

        [Fact]
        public async Task CreateOfferAsync_EndDateBeforeStartDate_ThrowsValidationException()
        {
            using AppDbContext db = DbFactory.CreateInMemoryDb();
            Guid mallId = Guid.NewGuid();
            Guid managerId = Guid.NewGuid();
            Guid storeId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Manager", PhoneNumber = "+962700100004", PasswordHash = "hash", Role = "manager", MallID = mallId });
            db.Managers.Add(new Manager { Id = managerId, Name = "Manager", Role = "manager", MallID = mallId });
            db.Stores.Add(new Store { Id = storeId, Name = "Nike", MallID = mallId });
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<ApiValidationException>(() =>
                CreateService(db).CreateOfferAsync(managerId, new CreateOfferRequest
                {
                    StoreId = storeId,
                    Title = "Bad Dates",
                    StartAt = now.AddDays(5),
                    EndAt = now.AddDays(1),
                    IsActive = true
                }));
        }

        [Fact]
        public async Task CreateOfferAsync_StoreScopedManager_UnassignedStore_ThrowsForbidden()
        {
            using AppDbContext db = DbFactory.CreateInMemoryDb();
            Guid mallId = Guid.NewGuid();
            Guid managerId = Guid.NewGuid();
            Guid assignedStore = Guid.NewGuid();
            Guid otherStore = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Manager", PhoneNumber = "+962700100005", PasswordHash = "hash", Role = "manager", MallID = mallId });
            db.Managers.Add(new Manager { Id = managerId, Name = "Manager", Role = "manager", MallID = mallId });
            db.Stores.AddRange(
                new Store { Id = assignedStore, Name = "Assigned", MallID = mallId },
                new Store { Id = otherStore,    Name = "Other",    MallID = mallId });
            db.Management.Add(new Management { ManagerId = managerId, StoreId = assignedStore, CreatedAt = now });
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<ApiForbiddenException>(() =>
                CreateService(db).CreateOfferAsync(managerId, new CreateOfferRequest
                {
                    StoreId = otherStore,
                    Title = "Forbidden Offer",
                    StartAt = now.AddDays(-1),
                    EndAt = now.AddDays(7),
                    IsActive = true
                }));
        }

        [Fact]
        public async Task CreateOfferAsync_RegularUser_ThrowsForbidden()
        {
            using AppDbContext db = DbFactory.CreateInMemoryDb();
            Guid mallId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();
            Guid storeId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700100006", PasswordHash = "hash", Role = "user", MallID = mallId });
            db.Stores.Add(new Store { Id = storeId, Name = "Nike", MallID = mallId });
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<ApiForbiddenException>(() =>
                CreateService(db).CreateOfferAsync(userId, new CreateOfferRequest
                {
                    StoreId = storeId,
                    Title = "Not Allowed",
                    StartAt = now.AddDays(-1),
                    EndAt = now.AddDays(7),
                    IsActive = true
                }));
        }

        [Fact]
        public async Task SetOfferStatusAsync_DeactivatesActiveOffer()
        {
            using AppDbContext db = DbFactory.CreateInMemoryDb();
            Guid mallId = Guid.NewGuid();
            Guid managerId = Guid.NewGuid();
            Guid storeId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Manager", PhoneNumber = "+962700100007", PasswordHash = "hash", Role = "manager", MallID = mallId });
            db.Managers.Add(new Manager { Id = managerId, Name = "Manager", Role = "manager", MallID = mallId });
            db.Stores.Add(new Store { Id = storeId, Name = "Nike", MallID = mallId });
            var offer = new Offer { StoreId = storeId, MallID = mallId, Title = "Active Offer", StartAt = now.AddDays(-1), EndAt = now.AddDays(7), IsActive = true, MadeAt = now };
            db.Offers.Add(offer);
            await db.SaveChangesAsync();

            var response = await CreateService(db).SetOfferStatusAsync(managerId, offer.Id, false);

            Assert.False(response.IsActive);
            Assert.False(db.Offers.Single().IsActive);
        }

        [Fact]
        public async Task DeleteOfferAsync_MallWideManager_RemovesOfferFromDatabase()
        {
            using AppDbContext db = DbFactory.CreateInMemoryDb();
            Guid mallId = Guid.NewGuid();
            Guid managerId = Guid.NewGuid();
            Guid storeId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Manager", PhoneNumber = "+962700100008", PasswordHash = "hash", Role = "manager", MallID = mallId });
            db.Managers.Add(new Manager { Id = managerId, Name = "Manager", Role = "manager", MallID = mallId });
            db.Stores.Add(new Store { Id = storeId, Name = "Nike", MallID = mallId });
            var offer = new Offer { StoreId = storeId, MallID = mallId, Title = "To Delete", StartAt = now.AddDays(-1), EndAt = now.AddDays(7), IsActive = true, MadeAt = now };
            db.Offers.Add(offer);
            await db.SaveChangesAsync();

            await CreateService(db).DeleteOfferAsync(managerId, offer.Id);

            Assert.Empty(db.Offers);
        }
    }
}
