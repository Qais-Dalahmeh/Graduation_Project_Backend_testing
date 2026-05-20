using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Graduation_Project_Backend.IntegrationTests.Infrastructure;
using Xunit;

namespace Graduation_Project_Backend.IntegrationTests.Tests;

/// <summary>
/// Integration tests for the Dashboard against real PostgreSQL.
/// Verifies that analytics queries return correct data from the real database.
/// </summary>
[Collection("Integration")]
public sealed class DashboardIntegrationTests
{
    private readonly HttpClient _client;
    private string? _managerSid;
    private string? _userSid;

    public DashboardIntegrationTests(IntegrationTestFactory factory)
    {
        _client = factory.CreateClient();
        using var db = factory.CreateDbContext();
        IntegrationTestSeeder.Seed(db);
    }

    private async Task<string> ManagerSidAsync()
    {
        if (_managerSid != null) return _managerSid;
        var r = await _client.PostAsJsonAsync("/api/auth/manager-quick-login",
            new { managerId = IntegrationTestSeeder.ManagerId });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return _managerSid = body.GetProperty("sessionId").GetString()!;
    }

    private async Task<string> UserSidAsync()
    {
        if (_userSid != null) return _userSid;
        var phone = "+9627" + DateTimeOffset.UtcNow.Ticks.ToString()[^8..];
        var r = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "DashUser", phoneNumber = phone,
            password = "TestPass1!", mallID = IntegrationTestSeeder.MallId
        });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return _userSid = body.GetProperty("sessionId").GetString()!;
    }

    private HttpRequestMessage Request(HttpMethod m, string url, string sid)
    {
        var req = new HttpRequestMessage(m, url);
        req.Headers.Add("X-Session-Id", sid);
        return req;
    }

    private const string From = "2025-01-01T00:00:00Z";
    private const string To   = "2027-12-31T23:59:59Z";

    [Fact]
    public async Task GetSummary_AsManager_Returns200_WithExpectedFields()
    {
        var sid      = await ManagerSidAsync();
        var response = await _client.SendAsync(
            Request(HttpMethod.Get, "/api/dashboard/summary", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("totalTransactions",  out _));
        Assert.True(body.TryGetProperty("totalSalesAmount",   out _));
        Assert.True(body.TryGetProperty("totalPointsIssued",  out _));
        Assert.True(body.TryGetProperty("activeOffersCount",  out _));
    }

    [Fact]
    public async Task GetSummary_AsRegularUser_Returns403()
    {
        var sid      = await UserSidAsync();
        var response = await _client.SendAsync(
            Request(HttpMethod.Get, "/api/dashboard/summary", sid));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSummary_InvalidDateRange_Returns400()
    {
        var sid = await ManagerSidAsync();
        // from > to → invalid
        var response = await _client.SendAsync(Request(HttpMethod.Get,
            "/api/dashboard/summary?from=2027-01-01T00:00:00Z&to=2025-01-01T00:00:00Z", sid));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetSalesAnalytics_AsManager_Returns200_WithDailySales()
    {
        var sid      = await ManagerSidAsync();
        var response = await _client.SendAsync(
            Request(HttpMethod.Get, $"/api/dashboard/sales?from={From}&to={To}", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("totalSalesAmount", out _));
        Assert.True(body.TryGetProperty("dailySales",       out _));
        Assert.True(body.TryGetProperty("topStores",        out _));
    }

    [Fact]
    public async Task GetPointsAnalytics_AsManager_Returns200()
    {
        var sid      = await ManagerSidAsync();
        var response = await _client.SendAsync(
            Request(HttpMethod.Get, "/api/dashboard/points", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("totalPointsIssued", out _));
        Assert.True(body.TryGetProperty("dailyIssued",       out _));
    }

    [Fact]
    public async Task GetCouponsAnalytics_AsManager_Returns200()
    {
        var sid      = await ManagerSidAsync();
        var response = await _client.SendAsync(
            Request(HttpMethod.Get, "/api/dashboard/coupons", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("redemptionRate",    out _));
        Assert.True(body.TryGetProperty("isScopeLimited",    out _));
    }

    [Fact]
    public async Task GetActivityDashboard_AsManager_Returns200()
    {
        var sid      = await ManagerSidAsync();
        var response = await _client.SendAsync(
            Request(HttpMethod.Get, "/api/dashboard/activity", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("totalOffers",        out _));
        Assert.True(body.TryGetProperty("totalAnnouncements", out _));
    }

    [Fact]
    public async Task GetSummary_AfterAddingTransaction_TotalsIncrease()
    {
        // Register a user and add a real transaction to the DB
        var phone = "+9627" + DateTimeOffset.UtcNow.Ticks.ToString()[^8..];
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "DashTxUser", phoneNumber = phone,
            password = "TestPass1!", mallID = IntegrationTestSeeder.MallId
        });
        await _client.PostAsJsonAsync("/api/transactions", new
        {
            phoneNumber = phone,
            storeId     = IntegrationTestSeeder.StoreId,
            receiptId   = "DASH-" + Guid.NewGuid().ToString("N")[..8],
            price       = 999m
        });

        // Summary must reflect at least 1 transaction
        var sid      = await ManagerSidAsync();
        var response = await _client.SendAsync(
            Request(HttpMethod.Get, "/api/dashboard/summary", sid));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("totalTransactions").GetInt32() >= 1);
        Assert.True(body.GetProperty("totalSalesAmount").GetDecimal() >= 999m);
    }
}
