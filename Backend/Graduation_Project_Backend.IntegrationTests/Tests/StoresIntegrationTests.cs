using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Graduation_Project_Backend.IntegrationTests.Infrastructure;
using Xunit;

namespace Graduation_Project_Backend.IntegrationTests.Tests;

/// <summary>
/// Integration tests for Stores CRUD against real PostgreSQL.
/// </summary>
[Collection("Integration")]
public sealed class StoresIntegrationTests
{
    private readonly HttpClient _client;
    private string? _userSid;
    private string? _managerSid;

    public StoresIntegrationTests(IntegrationTestFactory factory)
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
            name = "StoreUser", phoneNumber = phone,
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
    public async Task GetStores_AsUser_Returns200_WithSeededStore()
    {
        var sid      = await UserSidAsync();
        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/stores", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.True(body.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task GetStoreById_SeededStore_Returns200_WithCorrectData()
    {
        var sid      = await UserSidAsync();
        var response = await _client.SendAsync(
            Request(HttpMethod.Get, $"/api/stores/{IntegrationTestSeeder.StoreId}", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(IntegrationTestSeeder.StoreId.ToString(), body.GetProperty("id").GetString());
        Assert.Equal("Integration Test Store", body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetStoreById_NotFound_Returns404()
    {
        var sid      = await UserSidAsync();
        var response = await _client.SendAsync(
            Request(HttpMethod.Get, $"/api/stores/{Guid.NewGuid()}", sid));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateStore_AsManager_Returns201_AndPersistedToDb()
    {
        var sid = await ManagerSidAsync();
        var req = Request(HttpMethod.Post, "/api/stores", sid);
        req.Content = JsonContent.Create(new
        {
            name           = "New Integration Store",
            operatingHours = "9 AM - 9 PM",
            description    = "Created by integration test",
            floorNumber    = "2"
        });

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("New Integration Store", body.GetProperty("name").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("id").GetString()));
    }

    [Fact]
    public async Task UpdateStore_AsManager_Returns200_WithUpdatedName()
    {
        var sid = await ManagerSidAsync();

        // Create
        var create = Request(HttpMethod.Post, "/api/stores", sid);
        create.Content = JsonContent.Create(new { name = "Store To Update" });
        var created    = await _client.SendAsync(create);
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var storeId     = createdBody.GetProperty("id").GetString()!;

        // Update
        var update = Request(HttpMethod.Put, $"/api/stores/{storeId}", sid);
        update.Content = JsonContent.Create(new { name = "Updated Integration Store" });
        var response   = await _client.SendAsync(update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated Integration Store", body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task CreateStore_AsRegularUser_Returns403()
    {
        var sid = await UserSidAsync();
        var req = Request(HttpMethod.Post, "/api/stores", sid);
        req.Content = JsonContent.Create(new { name = "Unauthorized Store" });
        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
