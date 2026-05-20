using Graduation_Project_Backend.Data;
using Graduation_Project_Backend.Models.Entities;
using Graduation_Project_Backend.Models.User;
using Microsoft.AspNetCore.Identity;

namespace Graduation_Project_Backend.IntegrationTests.Infrastructure;

/// <summary>
/// Seeds base data into the REAL PostgreSQL database before integration tests run.
/// Idempotent — safe to call multiple times (checks if data already exists).
/// </summary>
public static class IntegrationTestSeeder
{
    // ── Fixed GUIDs so all tests can reference them ──────────────────────
    public static readonly Guid MallId       = Guid.Parse("A0000000-0000-0000-0000-000000000001");
    public static readonly Guid ManagerId    = Guid.Parse("A0000000-0000-0000-0000-000000000002");
    public static readonly Guid StoreId      = Guid.Parse("A0000000-0000-0000-0000-000000000003");
    public static readonly Guid FreeCouponId = Guid.Parse("A0000000-0000-0000-0000-000000000004");
    public static readonly Guid PaidCouponId = Guid.Parse("A0000000-0000-0000-0000-000000000005");

    public const string ManagerPhone    = "+962790000100";
    public const string ManagerPassword = "TestPass1!";

    public static void Seed(AppDbContext db)
    {
        // Idempotent guard — only seed once
        if (db.Malls.Any(m => m.Id == MallId)) return;

        var hasher = new PasswordHasher<UserProfile>();

        // ── Mall ──────────────────────────────────────────────────────────
        db.Malls.Add(new Mall
        {
            Id        = MallId,
            Name      = "Integration Test Mall",
            CreatedAt = DateTimeOffset.UtcNow
        });

        // ── Manager UserProfile ───────────────────────────────────────────
        var managerProfile = new UserProfile
        {
            Id           = ManagerId,
            Name         = "Integration Manager",
            PhoneNumber  = ManagerPhone,
            PasswordHash = string.Empty,
            Role         = "manager",
            MallID       = MallId
        };
        managerProfile.PasswordHash = hasher.HashPassword(managerProfile, ManagerPassword);
        db.UserProfiles.Add(managerProfile);

        // ── Manager row (no Management rows → IsMallWideManager = true) ──
        db.Managers.Add(new Manager
        {
            Id     = ManagerId,
            Name   = "Integration Manager",
            MallID = MallId,
            Role   = "manager"
        });

        // ── Store ─────────────────────────────────────────────────────────
        db.Stores.Add(new Store
        {
            Id             = StoreId,
            Name           = "Integration Test Store",
            MallID         = MallId,
            OperatingHours = "9 AM - 10 PM",
            Description    = "Seeded by IntegrationTests"
        });

        // ── Free coupon (CostPoint = null → no points required) ───────────
        db.Coupons.Add(new Coupon
        {
            Id          = FreeCouponId,
            Type        = "free_discount",
            IsActive    = true,
            StartAt     = DateTimeOffset.UtcNow.AddDays(-1),
            EndAt       = DateTimeOffset.UtcNow.AddDays(60),
            Discription = "Free coupon — no points needed",
            CostPoint   = null,
            MallID      = MallId,
            ManagerId   = ManagerId,
            CreatedAt   = DateTimeOffset.UtcNow
        });

        // ── Paid coupon (requires 100 points) ─────────────────────────────
        db.Coupons.Add(new Coupon
        {
            Id          = PaidCouponId,
            Type        = "paid_discount",
            IsActive    = true,
            StartAt     = DateTimeOffset.UtcNow.AddDays(-1),
            EndAt       = DateTimeOffset.UtcNow.AddDays(60),
            Discription = "Paid coupon — costs 100 points",
            CostPoint   = 100,
            MallID      = MallId,
            ManagerId   = ManagerId,
            CreatedAt   = DateTimeOffset.UtcNow
        });

        // ── Active announcement ────────────────────────────────────────────
        db.Announcements.Add(new Announcement
        {
            Id               = Guid.NewGuid(),
            Title            = "Integration Test Announcement",
            Content          = "Seeded for integration tests",
            AnnouncementType = "general",
            Priority         = "normal",
            IsActive         = true,
            IsPinned         = false,
            MallID           = MallId,
            ManagerId        = ManagerId,
            StartDate        = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate          = DateTimeOffset.UtcNow.AddDays(60),
            CreatedAt        = DateTimeOffset.UtcNow,
            UpdatedAt        = DateTimeOffset.UtcNow
        });

        db.SaveChanges();
    }
}
