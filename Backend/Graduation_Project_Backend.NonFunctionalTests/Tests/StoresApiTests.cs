using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Graduation_Project_Backend.NonFunctionalTests.Infrastructure;
using Xunit;

namespace Graduation_Project_Backend.NonFunctionalTests.Tests;

/// <summary>
/// NF-03: Stores API integration tests.
/// Covers listing, get-by-id, create, update — as manager and as user.
/// </summary>
public sealed class StoresApiTests : IClassFixture<NonFunctionalTestFactory>
{
    private readonly HttpClient _client;
    private string? _managerSid;
    private string? _userSid;

    public StoresApiTests(NonFunctionalTestFactory factory)
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
            name = "StoreUser", phoneNumber = phone, password = "TestPass1!",
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

    // ── GET /api/stores ───────────────────────────────────────────────────

    [Fact]
    public async Task GetStores_AsUser_Returns200WithArray()
    {
        var sid      = await UserSidAsync();
        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/stores", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.True(body.GetArrayLength() >= 1);
    }

    // ── GET /api/stores/{id} ──────────────────────────────────────────────

    [Fact]
    public async Task GetStoreById_ExistingId_Returns200WithDetails()
    {
        var sid      = await UserSidAsync();
        var response = await _client.SendAsync(Request(HttpMethod.Get,
            $"/api/stores/{TestSeeder.StoreId}", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(TestSeeder.StoreId.ToString(), body.GetProperty("id").GetString());
        Assert.Equal("NF Test Store", body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetStoreById_NotFound_Returns404()
    {
        var sid      = await UserSidAsync();
        var response = await _client.SendAsync(Request(HttpMethod.Get,
            $"/api/stores/{Guid.NewGuid()}", sid));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── GET /api/stores/manage ────────────────────────────────────────────

    [Fact]
    public async Task GetManagedStores_AsManager_Returns200WithArray()
    {
        var sid      = await ManagerSidAsync();
        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/stores/manage", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }

    // ── POST /api/stores ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateStore_AsManager_Returns201WithNewStore()
    {
        var sid = await ManagerSidAsync();
        var req = Request(HttpMethod.Post, "/api/stores", sid);
        req.Content = JsonContent.Create(new
        {
            name           = "Brand New Store",
            operatingHours = "8 AM - 9 PM",
            description    = "Created by NF test",
            floorNumber    = "3"
        });

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Brand New Store", body.GetProperty("name").GetString());
        Assert.True(body.GetProperty("id").GetString()!.Length > 0);
    }

    // ── PUT /api/stores/{id} ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateStore_AsManager_Returns200WithUpdatedName()
    {
        // Create a store first
        var sid    = await ManagerSidAsync();
        var create = Request(HttpMethod.Post, "/api/stores", sid);
        create.Content = JsonContent.Create(new { name = "Store To Update" });
        var created = await _client.SendAsync(create);
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var storeId     = createdBody.GetProperty("id").GetString()!;

        // Update it
        var update = Request(HttpMethod.Put, $"/api/stores/{storeId}", sid);
        update.Content = JsonContent.Create(new { name = "Updated Store Name" });
        var response = await _client.SendAsync(update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated Store Name", body.GetProperty("name").GetString());
    }
}
