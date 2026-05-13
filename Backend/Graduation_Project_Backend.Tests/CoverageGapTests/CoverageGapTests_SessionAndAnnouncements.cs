using Graduation_Project_Backend.DTOs.Announcements;
using Graduation_Project_Backend.Models.Entities;
using Graduation_Project_Backend.Models.User;
using Graduation_Project_Backend.Service;
using Graduation_Project_Backend.Service.Common;
using Graduation_Project_Backend.Service.Session;
using Graduation_Project_Backend.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Graduation_Project_Backend.Tests.CoverageGapTests;

/// <summary>
/// Coverage-gap tests for SessionService.GetSessionByIdAsync and
/// AnnouncementsService.GetManagedAnnouncementsAsync / SetAnnouncementStatusAsync.
/// </summary>
public sealed class CoverageGapTests_SessionAndAnnouncements
{
    // ── SessionService ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetSessionByIdAsync_ReturnsNull_ForNullInput()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        var service = new SessionService(db);

        var result = await service.GetSessionByIdAsync(null!);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetSessionByIdAsync_ReturnsNull_ForWhitespaceInput()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        var service = new SessionService(db);

        var result = await service.GetSessionByIdAsync("   ");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetSessionByIdAsync_ReturnsNull_WhenSessionNotFound()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        var service = new SessionService(db);

        var result = await service.GetSessionByIdAsync("nonexistent-session-id");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetSessionByIdAsync_ReturnsSession_WhenFound()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = DateTimeOffset.UtcNow });
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700700001", PasswordHash = "h", Role = "user", MallID = mallId });
        db.UserSessions.Add(new UserSession { Id = "test-session-abc", UserId = userId, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var service = new SessionService(db);
        var result = await service.GetSessionByIdAsync("test-session-abc");

        Assert.NotNull(result);
        Assert.Equal("test-session-abc", result.Id);
        Assert.Equal(userId, result.UserId);
    }

    [Fact]
    public async Task GetSessionByIdAsync_TrimsWhitespace_AndFindsSession()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = DateTimeOffset.UtcNow });
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700700002", PasswordHash = "h", Role = "user", MallID = mallId });
        db.UserSessions.Add(new UserSession { Id = "padded-session", UserId = userId, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var service = new SessionService(db);
        var result = await service.GetSessionByIdAsync("  padded-session  ");

        Assert.NotNull(result);
    }

    // ── AnnouncementsService helpers ─────────────────────────────────────────

    private static AnnouncementsService CreateAnnouncementsService(AppDbContext db)
    {
        var access = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        return new AnnouncementsService(db, access, NullLogger<AnnouncementsService>.Instance);
    }

    private static (Guid managerId, Guid mallId, Guid storeId) SeedMallWideManager(AppDbContext db)
    {
        Guid mallId = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();
        Guid storeId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "City Mall", CreatedAt = now });
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Mgr", PhoneNumber = "+962700700010", PasswordHash = "h", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = managerId, Name = "Mgr", Role = "manager", MallID = mallId });
        db.Stores.Add(new Store { Id = storeId, Name = "Store X", MallID = mallId });
        db.SaveChanges();

        return (managerId, mallId, storeId);
    }

    private static (Guid managerId, Guid mallId, Guid storeId) SeedStoreScopedManager(AppDbContext db)
    {
        Guid mallId = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();
        Guid storeId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "City Mall 2", CreatedAt = now });
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "StoreMgr", PhoneNumber = "+962700700011", PasswordHash = "h", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = managerId, Name = "StoreMgr", Role = "manager", MallID = mallId });
        db.Stores.Add(new Store { Id = storeId, Name = "Store Y", MallID = mallId });
        db.Management.Add(new Management { ManagerId = managerId, StoreId = storeId, CreatedAt = now });
        db.SaveChanges();

        return (managerId, mallId, storeId);
    }

    // ── SetAnnouncementStatusAsync ───────────────────────────────────────────

    [Fact]
    public async Task SetAnnouncementStatusAsync_DeactivatesActiveAnnouncement()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        var (managerId, mallId, _) = SeedMallWideManager(db);
        Guid announcementId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Announcements.Add(new Announcement
        {
            Id = announcementId,
            MallID = mallId,
            ManagerId = managerId,
            Title = "Hot Sale",
            Content = "Up to 50% off",
            AnnouncementType = "general",
            Priority = "normal",
            IsActive = true,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(5),
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        var service = CreateAnnouncementsService(db);
        var result = await service.SetAnnouncementStatusAsync(managerId, announcementId, false);

        Assert.False(result.IsActive);
        Assert.False(db.Announcements.Single(a => a.Id == announcementId).IsActive);
    }

    [Fact]
    public async Task SetAnnouncementStatusAsync_ActivatesInactiveAnnouncement()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        var (managerId, mallId, _) = SeedMallWideManager(db);
        Guid announcementId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Announcements.Add(new Announcement
        {
            Id = announcementId,
            MallID = mallId,
            ManagerId = managerId,
            Title = "Coming Soon",
            Content = "Big event!",
            AnnouncementType = "event",
            Priority = "high",
            IsActive = false,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(10),
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        var service = CreateAnnouncementsService(db);
        var result = await service.SetAnnouncementStatusAsync(managerId, announcementId, true);

        Assert.True(result.IsActive);
    }

    // ── GetManagedAnnouncementsAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetManagedAnnouncementsAsync_MallWideManager_ReturnsAllMallAnnouncements()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        var (managerId, mallId, storeId) = SeedMallWideManager(db);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Announcements.AddRange(
            new Announcement { Id = Guid.NewGuid(), MallID = mallId, ManagerId = managerId, Title = "A1", Content = "...", AnnouncementType = "general", Priority = "normal", IsActive = true, StartDate = now.AddDays(-1), EndDate = now.AddDays(5), CreatedAt = now, UpdatedAt = now },
            new Announcement { Id = Guid.NewGuid(), MallID = mallId, StoreId = storeId, ManagerId = managerId, Title = "A2", Content = "...", AnnouncementType = "promo", Priority = "high", IsActive = false, StartDate = now.AddDays(-2), EndDate = now.AddDays(3), CreatedAt = now.AddDays(-1), UpdatedAt = now.AddDays(-1) });
        await db.SaveChangesAsync();

        var service = CreateAnnouncementsService(db);
        var result = await service.GetManagedAnnouncementsAsync(managerId);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetManagedAnnouncementsAsync_StoreScopedManager_ReturnsOnlyAssignedStoreAnnouncements()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        var (managerId, mallId, storeId) = SeedStoreScopedManager(db);
        Guid otherStoreId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Stores.Add(new Store { Id = otherStoreId, Name = "Store Z", MallID = mallId });
        db.Announcements.AddRange(
            // Assigned store announcement — should appear
            new Announcement { Id = Guid.NewGuid(), MallID = mallId, StoreId = storeId, ManagerId = managerId, Title = "Mine", Content = "...", AnnouncementType = "general", Priority = "normal", IsActive = true, StartDate = now.AddDays(-1), EndDate = now.AddDays(5), CreatedAt = now, UpdatedAt = now },
            // Other store announcement — should NOT appear
            new Announcement { Id = Guid.NewGuid(), MallID = mallId, StoreId = otherStoreId, ManagerId = managerId, Title = "Not Mine", Content = "...", AnnouncementType = "general", Priority = "normal", IsActive = true, StartDate = now.AddDays(-1), EndDate = now.AddDays(5), CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();

        var service = CreateAnnouncementsService(db);
        var result = await service.GetManagedAnnouncementsAsync(managerId);

        Assert.Single(result);
        Assert.Equal("Mine", result[0].Title);
    }

    [Fact]
    public async Task GetManagedAnnouncementsAsync_ThrowsForbidden_ForRegularUser()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = DateTimeOffset.UtcNow });
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700700020", PasswordHash = "h", Role = "user", MallID = mallId });
        await db.SaveChangesAsync();

        var service = CreateAnnouncementsService(db);
        await Assert.ThrowsAsync<ApiForbiddenException>(() =>
            service.GetManagedAnnouncementsAsync(userId));
    }
}
