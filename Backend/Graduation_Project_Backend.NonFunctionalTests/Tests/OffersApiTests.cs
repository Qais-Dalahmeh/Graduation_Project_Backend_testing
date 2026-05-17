using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Graduation_Project_Backend.NonFunctionalTests.Infrastructure;
using Xunit;

namespace Graduation_Project_Backend.NonFunctionalTests.Tests;

/// <summary>
/// NF-05: Offers API integration tests.
/// Covers listing, create, update, and managed-offers endpoint.
/// </summary>
public sealed class OffersApiTests : IClassFixture<NonFunctionalTestFactory>
{
    private readonly HttpClient _client;
    private string? _managerSid;
    private string? _userSid;

    public OffersApiTests(NonFunctionalTestFactory factory)
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
            name = "OfferUser", phoneNumber = phone, password = "TestPass1!",
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

    // ── Tests ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOffers_AsUser_Returns200WithArray()
    {
        var sid      = await UserSidAsync();
        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/offers", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }

    [Fact]
    public async Task CreateOffer_AsManager_Returns201()
    {
        var sid = await ManagerSidAsync();
        var req = Request(HttpMethod.Post, "/api/offers", sid);
        req.Content = JsonContent.Create(new
        {
            storeId     = TestSeeder.StoreId,
            title       = "50% Off Everything",
            description = "Huge sale",
            startAt     = DateTimeOffset.UtcNow.AddDays(-1),
            endAt       = DateTimeOffset.UtcNow.AddDays(14),
            isActive    = true
        });

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("50% Off Everything", body.GetProperty("title").GetString());
        Assert.True(body.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task CreateOffer_MissingTitle_Returns400()
    {
        var sid = await ManagerSidAsync();
        var req = Request(HttpMethod.Post, "/api/offers", sid);
        req.Content = JsonContent.Create(new
        {
            storeId  = TestSeeder.StoreId,
            title    = "",                          // blank title → validation error
            startAt  = DateTimeOffset.UtcNow,
            endAt    = DateTimeOffset.UtcNow.AddDays(1)
        });

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateOffer_EndBeforeStart_Returns400()
    {
        var sid = await ManagerSidAsync();
        var req = Request(HttpMethod.Post, "/api/offers", sid);
        req.Content = JsonContent.Create(new
        {
            storeId  = TestSeeder.StoreId,
            title    = "Bad Dates Offer",
            startAt  = DateTimeOffset.UtcNow.AddDays(5),   // start after end
            endAt    = DateTimeOffset.UtcNow.AddDays(1)
        });

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetManagedOffers_AsManager_Returns200()
    {
        var sid      = await ManagerSidAsync();
        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/offers/manage", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }

    [Fact]
    public async Task UpdateOffer_AsManager_Returns200()
    {
        // Create an offer first
        var sid    = await ManagerSidAsync();
        var create = Request(HttpMethod.Post, "/api/offers", sid);
        create.Content = JsonContent.Create(new
        {
            storeId  = TestSeeder.StoreId,
            title    = "Offer To Update",
            startAt  = DateTimeOffset.UtcNow.AddDays(-1),
            endAt    = DateTimeOffset.UtcNow.AddDays(10)
        });
        var created    = await _client.SendAsync(create);
        var body       = await created.Content.ReadFromJsonAsync<JsonElement>();
        var offerId    = body.GetProperty("id").GetInt32();

        // Update it
        var update = Request(HttpMethod.Put, $"/api/offers/{offerId}", sid);
        update.Content = JsonContent.Create(new
        {
            storeId  = TestSeeder.StoreId,
            title    = "Updated Offer Title",
            startAt  = DateTimeOffset.UtcNow.AddDays(-1),
            endAt    = DateTimeOffset.UtcNow.AddDays(10),
            isActive = false
        });

        var response = await _client.SendAsync(update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updatedBody = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated Offer Title", updatedBody.GetProperty("title").GetString());
    }
}
