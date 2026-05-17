using Graduation_Project_Backend.DTOs.Announcements;
using Graduation_Project_Backend.Models.Entities;
using Graduation_Project_Backend.Models.User;
using Graduation_Project_Backend.Service;
using Graduation_Project_Backend.Service.Common;
using Graduation_Project_Backend.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Graduation_Project_Backend.Tests.FunctionalTests
{
    public sealed class AnnouncementsServiceTests
    {
        private static AnnouncementsService CreateService(AppDbContext db)
        {
            var access = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
            return new AnnouncementsService(db, access, NullLogger<AnnouncementsService>.Instance);
        }

        [Fact]
        public async Task GetVisibleAnnouncementsAsync_ReturnsOnlyActiveAnnouncementsWithinTimeWindow()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();
            Guid mgr = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700200001", PasswordHash = "hash", Role = "user", MallID = mallId });
            db.Announcements.AddRange(
                new Announcement { Id = Guid.NewGuid(), MallID = mallId, ManagerId = mgr, Title = "Current",  Content = "X", IsActive = true,  IsPinned = false, StartDate = now.AddDays(-1), EndDate = now.AddDays(1),  Priority = "normal", AnnouncementType = "general", CreatedAt = now, UpdatedAt = now },
                new Announcement { Id = Guid.NewGuid(), MallID = mallId, ManagerId = mgr, Title = "Past",     Content = "X", IsActive = true,  IsPinned = false, StartDate = now.AddDays(-3), EndDate = now.AddDays(-1), Priority = "normal", AnnouncementType = "general", CreatedAt = now, UpdatedAt = now },
                new Announcement { Id = Guid.NewGuid(), MallID = mallId, ManagerId = mgr, Title = "Future",   Content = "X", IsActive = true,  IsPinned = false, StartDate = now.AddDays(1),  EndDate = now.AddDays(3),  Priority = "normal", AnnouncementType = "general", CreatedAt = now, UpdatedAt = now },
                new Announcement { Id = Guid.NewGuid(), MallID = mallId, ManagerId = mgr, Title = "Disabled", Content = "X", IsActive = false, IsPinned = false, StartDate = now.AddDays(-1), EndDate = now.AddDays(1),  Priority = "normal", AnnouncementType = "general", CreatedAt = now, UpdatedAt = now });
            await db.SaveChangesAsync();

            var result = await CreateService(db).GetVisibleAnnouncementsAsync(userId);

            Assert.Single(result);
            Assert.Equal("Current", result[0].Title);
        }

        [Fact]
        public async Task GetVisibleAnnouncementsAsync_PinnedAnnouncementsAppearFirst()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();
            Guid mgr = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700200002", PasswordHash = "hash", Role = "user", MallID = mallId });
            db.Announcements.AddRange(
                new Announcement { Id = Guid.NewGuid(), MallID = mallId, ManagerId = mgr, Title = "Normal", Content = "X", IsActive = true, IsPinned = false, StartDate = now.AddDays(-1), EndDate = now.AddDays(1), Priority = "normal", AnnouncementType = "general", CreatedAt = now, UpdatedAt = now },
                new Announcement { Id = Guid.NewGuid(), MallID = mallId, ManagerId = mgr, Title = "Pinned", Content = "X", IsActive = true, IsPinned = true,  StartDate = now.AddDays(-1), EndDate = now.AddDays(1), Priority = "normal", AnnouncementType = "general", CreatedAt = now, UpdatedAt = now });
            await db.SaveChangesAsync();

            var result = await CreateService(db).GetVisibleAnnouncementsAsync(userId);

            Assert.Equal(2, result.Count);
            Assert.Equal("Pinned", result[0].Title);
            Assert.Equal("Normal", result[1].Title);
        }

        [Fact]
        public async Task GetVisibleAnnouncementsAsync_HighPriorityBeforeNormalWhenBothUnpinned()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();
            Guid mgr = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700200003", PasswordHash = "hash", Role = "user", MallID = mallId });
            db.Announcements.AddRange(
                new Announcement { Id = Guid.NewGuid(), MallID = mallId, ManagerId = mgr, Title = "Normal Priority", Content = "X", IsActive = true, IsPinned = false, StartDate = now.AddDays(-2), EndDate = now.AddDays(1), Priority = "normal", AnnouncementType = "general", CreatedAt = now.AddDays(-2), UpdatedAt = now.AddDays(-2) },
                new Announcement { Id = Guid.NewGuid(), MallID = mallId, ManagerId = mgr, Title = "High Priority",   Content = "X", IsActive = true, IsPinned = false, StartDate = now.AddDays(-1), EndDate = now.AddDays(1), Priority = "high",   AnnouncementType = "general", CreatedAt = now.AddDays(-1), UpdatedAt = now.AddDays(-1) });
            await db.SaveChangesAsync();

            var result = await CreateService(db).GetVisibleAnnouncementsAsync(userId);

            Assert.Equal(2, result.Count);
            Assert.Equal("High Priority", result[0].Title);
        }

        [Fact]
        public async Task CreateAnnouncementAsync_ValidRequest_AppliesDefaultTypeAndPriority()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            Guid managerId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Manager", PhoneNumber = "+962700200004", PasswordHash = "hash", Role = "manager", MallID = mallId });
            db.Managers.Add(new Manager { Id = managerId, Name = "Manager", Role = "manager", MallID = mallId });
            await db.SaveChangesAsync();

            var response = await CreateService(db).CreateAnnouncementAsync(managerId, new CreateAnnouncementRequest
            {
                Title = "Grand Opening",
                Content = "Come visit us!",
                StartDate = now.AddDays(-1),
                EndDate = now.AddDays(7),
                IsActive = true,
                IsPinned = false
            });

            Assert.Equal("Grand Opening", response.Title);
            Assert.Equal("general", response.AnnouncementType);
            Assert.Equal("normal", response.Priority);
        }

        [Fact]
        public async Task CreateAnnouncementAsync_EndDateBeforeStartDate_ThrowsValidation()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            Guid managerId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Manager", PhoneNumber = "+962700200005", PasswordHash = "hash", Role = "manager", MallID = mallId });
            db.Managers.Add(new Manager { Id = managerId, Name = "Manager", Role = "manager", MallID = mallId });
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<ApiValidationException>(() =>
                CreateService(db).CreateAnnouncementAsync(managerId, new CreateAnnouncementRequest
                {
                    Title = "Bad Dates",
                    Content = "Content",
                    StartDate = now.AddDays(5),
                    EndDate = now.AddDays(1),
                    IsActive = true,
                    IsPinned = false
                }));
        }

        [Fact]
        public async Task CreateAnnouncementAsync_StoreScopedManager_WithoutStoreId_ThrowsForbidden()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            Guid managerId = Guid.NewGuid();
            Guid storeId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Manager", PhoneNumber = "+962700200006", PasswordHash = "hash", Role = "manager", MallID = mallId });
            db.Managers.Add(new Manager { Id = managerId, Name = "Manager", Role = "manager", MallID = mallId });
            db.Stores.Add(new Store { Id = storeId, Name = "Assigned Store", MallID = mallId });
            db.Management.Add(new Management { ManagerId = managerId, StoreId = storeId, CreatedAt = now });
            await db.SaveChangesAsync();

            // Store-scoped manager must supply a StoreId â€” null means mall-wide which is forbidden
            await Assert.ThrowsAsync<ApiForbiddenException>(() =>
                CreateService(db).CreateAnnouncementAsync(managerId, new CreateAnnouncementRequest
                {
                    StoreId = null,
                    Title = "Mall-Wide Attempt",
                    Content = "Content",
                    StartDate = now.AddDays(-1),
                    EndDate = now.AddDays(7),
                    IsActive = true,
                    IsPinned = false
                }));
        }

        [Fact]
        public async Task SetAnnouncementPinAsync_PinsExistingAnnouncement()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            Guid managerId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Manager", PhoneNumber = "+962700200007", PasswordHash = "hash", Role = "manager", MallID = mallId });
            db.Managers.Add(new Manager { Id = managerId, Name = "Manager", Role = "manager", MallID = mallId });
            var announcementId = Guid.NewGuid();
            db.Announcements.Add(new Announcement { Id = announcementId, MallID = mallId, ManagerId = managerId, Title = "News", Content = "X", IsActive = true, IsPinned = false, StartDate = now.AddDays(-1), EndDate = now.AddDays(7), Priority = "normal", AnnouncementType = "general", CreatedAt = now, UpdatedAt = now });
            await db.SaveChangesAsync();

            var response = await CreateService(db).SetAnnouncementPinAsync(managerId, announcementId, true);

            Assert.True(response.IsPinned);
            Assert.True(db.Announcements.Single().IsPinned);
        }

        [Fact]
        public async Task DeleteAnnouncementAsync_RemovesAnnouncementFromDatabase()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            Guid managerId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Manager", PhoneNumber = "+962700200008", PasswordHash = "hash", Role = "manager", MallID = mallId });
            db.Managers.Add(new Manager { Id = managerId, Name = "Manager", Role = "manager", MallID = mallId });
            var announcementId = Guid.NewGuid();
            db.Announcements.Add(new Announcement { Id = announcementId, MallID = mallId, ManagerId = managerId, Title = "To Delete", Content = "X", IsActive = true, IsPinned = false, StartDate = now.AddDays(-1), EndDate = now.AddDays(7), Priority = "normal", AnnouncementType = "general", CreatedAt = now, UpdatedAt = now });
            await db.SaveChangesAsync();

            await CreateService(db).DeleteAnnouncementAsync(managerId, announcementId);

            Assert.Empty(db.Announcements);
        }

        [Fact]
        public async Task GetManagedAnnouncementsAsync_RegularUser_ThrowsForbidden()
        {
            using AppDbContext db = TestInfrastructure.CreateDbContext();
            Guid mallId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
            db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700200009", PasswordHash = "hash", Role = "user", MallID = mallId });
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<ApiForbiddenException>(() =>
                CreateService(db).GetManagedAnnouncementsAsync(userId));
        }
    }
}

