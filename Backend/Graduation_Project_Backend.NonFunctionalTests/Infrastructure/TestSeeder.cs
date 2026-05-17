using Graduation_Project_Backend.Data;
using Graduation_Project_Backend.Models.Entities;
using Graduation_Project_Backend.Models.User;
using Microsoft.AspNetCore.Identity;

namespace Graduation_Project_Backend.NonFunctionalTests.Infrastructure;

/// <summary>
/// Seeds a minimal but complete dataset into the InMemory DB before each test class.
/// Idempotent — safe to call multiple times.
/// </summary>
public static class TestSeeder
{
    // ── Well-known IDs (fixed so tests can reference them) ───────────────
    public static readonly Guid MallId       = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid ManagerId    = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid StoreId      = Guid.Parse("30000000-0000-0000-0000-000000000003");
    public static readonly Guid FreeCouponId = Guid.Parse("40000000-0000-0000-0000-000000000004");

    public const string ManagerPhone    = "+962790000001";
    public const string ManagerPassword = "TestPass1!";

    public static void Seed(AppDbContext db)
    {
        if (db.Malls.Any()) return;   // already seeded for this DB instance

        var hasher = new PasswordHasher<UserProfile>();

        // Mall
        db.Malls.Add(new Mall
        {
            Id        = MallId,
            Name      = "NF Test Mall",
            CreatedAt = DateTimeOffset.UtcNow
        });

        // Manager UserProfile
        var managerProfile = new UserProfile
        {
            Id           = ManagerId,
            Name         = "NF Manager",
            PhoneNumber  = ManagerPhone,
            PasswordHash = string.Empty,
            Role         = "manager",
            MallID       = MallId
        };
        managerProfile.PasswordHash = hasher.HashPassword(managerProfile, ManagerPassword);
        db.UserProfiles.Add(managerProfile);

        // Manager row
        db.Managers.Add(new Manager
        {
            Id     = ManagerId,
            Name   = "NF Manager",
            MallID = MallId,
            Role   = "manager"
        });

        // Store
        db.Stores.Add(new Store
        {
            Id             = StoreId,
            Name           = "NF Test Store",
            MallID         = MallId,
            OperatingHours = "9 AM – 10 PM",
            Description    = "Seeded by NonFunctionalTests"
        });

        // Note: no Management row → manager has no assigned stores → IsMallWideManager = true

        // Free coupon (no points cost)
        db.Coupons.Add(new Coupon
        {
            Id          = FreeCouponId,
            Type        = "discount",
            IsActive    = true,
            StartAt     = DateTimeOffset.UtcNow.AddDays(-1),
            EndAt       = DateTimeOffset.UtcNow.AddDays(30),
            Discription = "Free coupon for NF tests",
            CostPoint   = null,
            MallID      = MallId,
            ManagerId   = ManagerId,
            CreatedAt   = DateTimeOffset.UtcNow
        });

        // Active announcement
        db.Announcements.Add(new Announcement
        {
            Id               = Guid.NewGuid(),
            Title            = "Seeded Announcement",
            Content          = "Content for NF tests",
            AnnouncementType = "general",
            Priority         = "normal",
            IsActive         = true,
            IsPinned         = false,
            MallID           = MallId,
            ManagerId        = ManagerId,
            StartDate        = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate          = DateTimeOffset.UtcNow.AddDays(30),
            CreatedAt        = DateTimeOffset.UtcNow,
            UpdatedAt        = DateTimeOffset.UtcNow
        });

        db.SaveChanges();
    }
}
