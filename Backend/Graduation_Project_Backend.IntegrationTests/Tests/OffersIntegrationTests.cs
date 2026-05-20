using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Graduation_Project_Backend.IntegrationTests.Infrastructure;
using Xunit;

namespace Graduation_Project_Backend.IntegrationTests.Tests;

/// <summary>
/// Integration tests for Offers CRUD against real PostgreSQL.
/// </summary>
[Collection("Integration")]
public sealed class OffersIntegrationTests
{
    private readonly HttpClient _client;
    private string? _userSid;
    private string? _managerSid;

    public OffersIntegrationTests(IntegrationTestFactory factory)
    {
        _client = factory.CreateClient();
        using var db = factory.CreateDbContext();
        IntegrationTestSeeder.Seed(db);
    }

    private async Task<string> UserSidAsync()
    {
        if (_userSid != null) return _userSid;
        var phone = "+9627" + DateTimeOffset.UtcNow.Ticks.ToString()[^8..];
        var r = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "OfferUser", phoneNumber = phone,
            password = "TestPass1!", mallID = IntegrationTestSeeder.MallId
        });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return _userSid = body.GetProperty("sessionId").GetString()!;
    }

    private async Task<string> ManagerSidAsync()
    {
        if (_managerSid != null) return _managerSid;
        var r = await _client.PostAsJsonAsync("/api/auth/manager-quick-login",
            new { managerId = IntegrationTestSeeder.ManagerId });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return _managerSid = body.GetProperty("sessionId").GetString()!;
    }

    private HttpRequestMessage Request(HttpMethod m, string url, string sid)
    {
        var req = new HttpRequestMessage(m, url);
        req.Headers.Add("X-Session-Id", sid);
        return req;
    }

    [Fact]
    public async Task GetOffers_AsUser_Returns200_WithArray()
    {
        var sid      = await UserSidAsync();
        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/offers", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }

    [Fact]
    public async Task CreateOffer_AsManager_Returns201_WithCorrectData()
    {
        var sid = await ManagerSidAsync();
        var req = Request(HttpMethod.Post, "/api/offers", sid);
        req.Content = JsonContent.Create(new
        {
            storeId     = IntegrationTestSeeder.StoreId,
            title       = "Integration Test Offer",
            description = "50% off everything",
            startAt     = DateTimeOffset.UtcNow.AddDays(-1),
            endAt       = DateTimeOffset.UtcNow.AddDays(30),
            isActive    = true
        });

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Integration Test Offer", body.GetProperty("title").GetString());
        Assert.True(body.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task CreateOffer_StartAfterEnd_Returns400()
    {
        var sid = await ManagerSidAsync();
        var req = Request(HttpMethod.Post, "/api/offers", sid);
        req.Content = JsonContent.Create(new
        {
            storeId  = IntegrationTestSeeder.StoreId,
            title    = "Bad Dates Offer",
            startAt  = DateTimeOffset.UtcNow.AddDays(10),  // start > end
            endAt    = DateTimeOffset.UtcNow.AddDays(1)
        });

        var response = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateOffer_AsManager_Returns200_WithUpdatedTitle()
    {
        var sid = await ManagerSidAsync();

        // Create
        var create = Request(HttpMethod.Post, "/api/offers", sid);
        create.Content = JsonContent.Create(new
        {
            storeId  = IntegrationTestSeeder.StoreId,
            title    = "Offer To Update",
            startAt  = DateTimeOffset.UtcNow,
            endAt    = DateTimeOffset.UtcNow.AddDays(30)
        });
        var created    = await _client.SendAsync(create);
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var offerId     = createdBody.GetProperty("id").GetInt32();

        // Update
        var update = Request(HttpMethod.Put, $"/api/offers/{offerId}", sid);
        update.Content = JsonContent.Create(new
        {
            storeId = IntegrationTestSeeder.StoreId,
            title   = "Updated Offer Title",
            startAt = DateTimeOffset.UtcNow,
            endAt   = DateTimeOffset.UtcNow.AddDays(30)
        });
        var response = await _client.SendAsync(update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated Offer Title", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task DeleteOffer_AsManager_Returns204()
    {
        var sid = await ManagerSidAsync();

        // Create
        var create = Request(HttpMethod.Post, "/api/offers", sid);
        create.Content = JsonContent.Create(new
        {
            storeId = IntegrationTestSeeder.StoreId,
            title   = "Offer To Delete",
            startAt = DateTimeOffset.UtcNow,
            endAt   = DateTimeOffset.UtcNow.AddDays(30)
        });
        var created    = await _client.SendAsync(create);
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var offerId     = createdBody.GetProperty("id").GetInt32();

        // Delete
        var response = await _client.SendAsync(
            Request(HttpMethod.Delete, $"/api/offers/{offerId}", sid));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
