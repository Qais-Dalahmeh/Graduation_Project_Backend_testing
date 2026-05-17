using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Graduation_Project_Backend.NonFunctionalTests.Infrastructure;
using Xunit;

namespace Graduation_Project_Backend.NonFunctionalTests.Tests;

/// <summary>
/// NF-07: Dashboard API integration tests.
/// Covers summary, sales, points, coupons, and activity endpoints (manager only).
/// </summary>
public sealed class DashboardApiTests : IClassFixture<NonFunctionalTestFactory>
{
    private readonly HttpClient _client;
    private string? _managerSid;
    private string? _userSid;

    public DashboardApiTests(NonFunctionalTestFactory factory)
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

    private async Task<string> UserSidAsync()
    {
        if (_userSid != null) return _userSid;
        var phone = "+9627" + DateTimeOffset.UtcNow.Ticks.ToString()[^8..];
        var r     = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "DashUser", phoneNumber = phone, password = "TestPass1!",
            mallID = TestSeeder.MallId
        });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return _userSid = body.GetProperty("sessionId").GetString()!;
    }

    private HttpRequestMessage Request(HttpMethod method, string url, string sid)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Session-Id", sid);
        return req;
    }

    // ── GET /api/dashboard/summary ────────────────────────────────────────

    [Fact]
    public async Task GetSummary_AsManager_Returns200WithFields()
    {
        var sid      = await ManagerSidAsync();
        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/dashboard/summary", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("totalTransactions", out _));
        Assert.True(body.TryGetProperty("totalSalesAmount",  out _));
        Assert.True(body.TryGetProperty("totalPointsIssued", out _));
    }

    [Fact]
    public async Task GetSummary_AsUser_Returns403()
    {
        var sid      = await UserSidAsync();
        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/dashboard/summary", sid));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSummary_WithValidDateRange_Returns200()
    {
        var sid = await ManagerSidAsync();
        var url = "/api/dashboard/summary?from=2025-01-01T00:00:00Z&to=2027-01-01T00:00:00Z";
        var response = await _client.SendAsync(Request(HttpMethod.Get, url, sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSummary_WithInvalidDateRange_Returns400()
    {
        var sid = await ManagerSidAsync();
        // from > to → invalid
        var url = "/api/dashboard/summary?from=2027-01-01T00:00:00Z&to=2025-01-01T00:00:00Z";
        var response = await _client.SendAsync(Request(HttpMethod.Get, url, sid));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── GET /api/dashboard/sales ──────────────────────────────────────────

    [Fact]
    public async Task GetSales_AsManager_Returns200WithArrays()
    {
        var sid      = await ManagerSidAsync();
        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/dashboard/sales", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("dailySales", out var ds)  && ds.ValueKind == JsonValueKind.Array);
        Assert.True(body.TryGetProperty("topStores",  out var ts)  && ts.ValueKind == JsonValueKind.Array);
    }

    // ── GET /api/dashboard/points ─────────────────────────────────────────

    [Fact]
    public async Task GetPoints_AsManager_Returns200WithArrays()
    {
        var sid      = await ManagerSidAsync();
        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/dashboard/points", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("dailyIssued",   out var di) && di.ValueKind == JsonValueKind.Array);
        Assert.True(body.TryGetProperty("dailyRedeemed", out var dr) && dr.ValueKind == JsonValueKind.Array);
    }

    // ── GET /api/dashboard/coupons ────────────────────────────────────────

    [Fact]
    public async Task GetCoupons_AsManager_Returns200()
    {
        var sid      = await ManagerSidAsync();
        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/dashboard/coupons", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("isScopeLimited", out _));
    }

    // ── GET /api/dashboard/activity ───────────────────────────────────────

    [Fact]
    public async Task GetActivity_AsManager_Returns200WithFields()
    {
        var sid      = await ManagerSidAsync();
        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/dashboard/activity", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("totalOffers",        out _));
        Assert.True(body.TryGetProperty("totalAnnouncements", out _));
        Assert.True(body.TryGetProperty("categoryDistribution", out var cd)
            && cd.ValueKind == JsonValueKind.Array);
    }
}
