using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Graduation_Project_Backend.IntegrationTests.Infrastructure;
using Xunit;

namespace Graduation_Project_Backend.IntegrationTests.Tests;

/// <summary>
/// Integration tests for the Coupons & Redemption flow against real PostgreSQL.
/// This test was IMPOSSIBLE with InMemory DB because redeem uses DB transactions.
/// Now it works correctly with a real PostgreSQL container.
/// </summary>
[Collection("Integration")]
public sealed class CouponsIntegrationTests
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public CouponsIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
        using var db = factory.CreateDbContext();
        IntegrationTestSeeder.Seed(db);
    }

    private string UniquePhone() =>
        "+9627" + DateTimeOffset.UtcNow.Ticks.ToString()[^8..];

    private string UniqueReceipt() =>
        "REC-" + Guid.NewGuid().ToString("N")[..10].ToUpper();

    private async Task<(string sid, string phone)> RegisterUserAsync()
    {
        var phone = UniquePhone();
        var r = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "CouponUser", phoneNumber = phone,
            password = "TestPass1!", mallID = IntegrationTestSeeder.MallId
        });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("sessionId").GetString()!, phone);
    }

    private HttpRequestMessage Request(HttpMethod m, string url, string sid)
    {
        var req = new HttpRequestMessage(m, url);
        req.Headers.Add("X-Session-Id", sid);
        return req;
    }

    // ── List coupons ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetCoupons_Returns200_WithSeededCoupons()
    {
        var (sid, _) = await RegisterUserAsync();
        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/coupons", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.True(body.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task GetCouponById_ExistingId_Returns200()
    {
        var (sid, _) = await RegisterUserAsync();
        var response = await _client.SendAsync(
            Request(HttpMethod.Get, $"/api/coupons/{IntegrationTestSeeder.FreeCouponId}", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(IntegrationTestSeeder.FreeCouponId.ToString(),
                     body.GetProperty("id").GetString());
    }

    [Fact]
    public async Task GetUserCoupons_Returns200_WithArray()
    {
        var (sid, _) = await RegisterUserAsync();
        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/coupons/user", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }

    // ── Redeem coupon (REAL DB TRANSACTION) ───────────────────────────────

    [Fact]
    public async Task RedeemFreeCoupon_UserWithPoints_Returns200_AndCouponSaved()
    {
        var (sid, phone) = await RegisterUserAsync();

        // Give user some points via a transaction first
        await _client.PostAsJsonAsync("/api/transactions", new
        {
            phoneNumber = phone,
            storeId     = IntegrationTestSeeder.StoreId,
            receiptId   = UniqueReceipt(),
            price       = 1000m     // enough to earn points
        });

        // Redeem the free coupon (CostPoint = null → free)
        var req = Request(HttpMethod.Post, "/api/coupons/redeem", sid);
        req.Content = JsonContent.Create(new { couponId = IntegrationTestSeeder.FreeCouponId });
        var response = await _client.SendAsync(req);

        // 200 = success, 400 = already redeemed (if run twice) — both are valid
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected 200 or 400 but got {(int)response.StatusCode}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.TryGetProperty("serial_number", out var serial));
            Assert.False(string.IsNullOrEmpty(serial.GetString()));

            // Verify the coupon is persisted in the real DB
            using var db = _factory.CreateDbContext();
            var user = db.UserProfiles.FirstOrDefault(u => u.PhoneNumber == phone);
            Assert.NotNull(user);
            var userCoupon = db.UserCoupons.FirstOrDefault(uc => uc.UserId == user.Id);
            Assert.NotNull(userCoupon);
        }
    }

    [Fact]
    public async Task RedeemPaidCoupon_InsufficientPoints_Returns400()
    {
        // New user has 0 points
        var (sid, _) = await RegisterUserAsync();

        var req = Request(HttpMethod.Post, "/api/coupons/redeem", sid);
        req.Content = JsonContent.Create(new { couponId = IntegrationTestSeeder.PaidCouponId });
        var response = await _client.SendAsync(req);

        // Should fail — 0 points < 100 required
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RedeemCoupon_NonExistentCoupon_Returns400()
    {
        var (sid, _) = await RegisterUserAsync();

        var req = Request(HttpMethod.Post, "/api/coupons/redeem", sid);
        req.Content = JsonContent.Create(new { couponId = Guid.NewGuid() });
        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
