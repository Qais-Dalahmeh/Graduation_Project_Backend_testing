using Graduation_Project_Backend.Data;
using Graduation_Project_Backend.DTOs;
using Graduation_Project_Backend.Models.Entities;
using Graduation_Project_Backend.Models.User;
using Graduation_Project_Backend.Service;
using Graduation_Project_Backend.Service.Common;
using Graduation_Project_Backend.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Graduation_Project_Backend.Service;

/// <summary>
/// Test facade that exposes service methods (including internals) for unit/integration tests.
/// </summary>
public sealed class ServiceClass
{
    private readonly AppDbContext _db;
    private readonly PhoneNumberService _phone = new();

    public ServiceClass(AppDbContext db)
    {
        _db = db;
    }

    private RewardsService CreateRewardsService() => new RewardsService(
        _db,
        _phone,
        new NoOpUserPointsUpdatesService(),
        new UserAccessService(_db, NullLogger<UserAccessService>.Instance));

    // ── Phone normalization ──────────────────────────────────────────────────
    public string NormalizePhone(string phoneNumber) => _phone.Normalize(phoneNumber);

    // ── Points helpers (expose internal logic for unit tests) ────────────────
    public void AddPoints(UserProfile user, int points)
        => user.TotalPoints += points;

    public void DeductPoints(UserProfile user, int points)
    {
        if (user.TotalPoints < points)
            throw new InvalidOperationException("Not enough points");
        user.TotalPoints -= points;
    }

    // ── User helpers ─────────────────────────────────────────────────────────
    public Task<UserProfile?> GetUserByIdAsync(Guid userId)
        => _db.UserProfiles.SingleOrDefaultAsync(u => u.Id == userId);

    // ── Rewards / coupons ────────────────────────────────────────────────────
    public Task<UserCoupon> RedeemCouponAsync(Guid userId, Guid couponId)
        => CreateRewardsService().RedeemCouponAsync(userId, couponId);

    public Task<UserCoupon> RedeemCouponBySerialAsync(string serialNumber)
        => CreateRewardsService().RedeemCouponBySerialAsync(serialNumber);

    public Task<TransactionResultDto> ProcessTransactionAsync(
        string phoneNumber,
        Guid storeId,
        string receiptId,
        string? receiptDescription,
        decimal price)
        => CreateRewardsService().ProcessTransactionAsync(phoneNumber, storeId, receiptId, receiptDescription, price);

    // ── Store helpers ────────────────────────────────────────────────────────
    public async Task<Store> CreateStoreAsync(string name)
    {
        name = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Store name cannot be empty.");

        var store = new Store { Id = Guid.NewGuid(), Name = name };
        _db.Stores.Add(store);
        await _db.SaveChangesAsync();
        return store;
    }

    public Task<Store?> GetStoreByIdAsync(Guid storeId)
        => _db.Stores.SingleOrDefaultAsync(s => s.Id == storeId);
}
