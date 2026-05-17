using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Graduation_Project_Backend.NonFunctionalTests.Infrastructure;
using Xunit;

namespace Graduation_Project_Backend.NonFunctionalTests.Tests;

/// <summary>
/// NF-06: Rewards & Coupons API integration tests.
/// Covers listing coupons, redeeming free coupons, and processing transactions.
/// </summary>
public sealed class RewardsApiTests : IClassFixture<NonFunctionalTestFactory>
{
    private readonly HttpClient _client;
    private string? _managerSid;
    private string? _userSid;

    public RewardsApiTests(NonFunctionalTestFactory factory)
    {
        _client = factory.CreateClient();
        using var db = factory.CreateDbContext();
        TestSeeder.Seed(db);
    }

    private async Task<string> ManagerSidAsync()
    {
        if (_managerSid != null) return _managerSid;
        var r    = await _client.PostAsJsonAsync("/api/auth/manager-quick-login", new { managerId = TestSeeder.ManagerId });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return _managerSid = body.GetProperty("sessionId").GetString()!;
    }

    private async Task<(string sid, string phone)> RegisterUserAsync()
    {
        var phone = "+9627" + DateTimeOffset.UtcNow.Ticks.ToString()[^8..];
        var r = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "RewardUser", phoneNumber = phone, password = "TestPass1!",
            mallID = TestSeeder.MallId
        });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("sessionId").GetString()!, phone);
    }

    private async Task<string> UserSidAsync()
    {
        if (_userSid != null) return _userSid;
        return _userSid = (await RegisterUserAsync()).sid;
    }

    private HttpRequestMessage Request(HttpMethod method, string url, string sid)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Session-Id", sid);
        return req;
    }

    // ── GET /api/coupons ──────────────────────────────────────────────────

    [Fact]
    public async Task GetCoupons_AsUser_Returns200WithArray()
    {
        var sid      = await UserSidAsync();
        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/coupons", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }

    // ── GET /api/coupons/user ─────────────────────────────────────────────

    [Fact]
    public async Task GetUserCoupons_AsUser_Returns200WithArray()
    {
        var sid      = await UserSidAsync();
        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/coupons/user", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }

    // ── POST /api/coupons/redeem — validation paths ───────────────────────
    // Note: full redeem flow requires DB transaction support (not available
    // with InMemory provider). These tests cover the validation-only paths.

    [Fact]
    public async Task RedeemCoupon_EmptyBody_Returns400()
    {
        var sid = await UserSidAsync();
        var req = Request(HttpMethod.Post, "/api/coupons/redeem", sid);
        req.Content = JsonContent.Create(new { couponId = Guid.Empty }); // empty GUID → validation fail

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RedeemCoupon_NonExistentCoupon_Returns400()
    {
        var sid = await UserSidAsync();
        var req = Request(HttpMethod.Post, "/api/coupons/redeem", sid);
        req.Content = JsonContent.Create(new { couponId = Guid.NewGuid() }); // does not exist

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── POST /api/transactions — validation paths ─────────────────────────
    // (Full flow requires DB transaction support; tests cover input validation.)

    [Fact]
    public async Task AddTransaction_NullBody_Returns400()
    {
        var mgrSid = await ManagerSidAsync();
        var req    = Request(HttpMethod.Post, "/api/transactions", mgrSid);
        // no Content → body is null
        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddTransaction_MissingPhoneNumber_Returns400()
    {
        var mgrSid = await ManagerSidAsync();
        var req    = Request(HttpMethod.Post, "/api/transactions", mgrSid);
        req.Content = JsonContent.Create(new
        {
            phoneNumber = "",              // blank → validation fail
            storeId     = TestSeeder.StoreId,
            receiptId   = "REC-001",
            price       = 50.00m
        });

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
