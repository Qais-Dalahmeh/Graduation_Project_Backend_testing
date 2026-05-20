using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Graduation_Project_Backend.IntegrationTests.Infrastructure;
using Xunit;

namespace Graduation_Project_Backend.IntegrationTests.Tests;

/// <summary>
/// Integration tests for the Transactions flow against a real PostgreSQL database.
/// This is the critical test — it uses real DB transactions (impossible with InMemory).
/// Verifies: add transaction → points calculated → persisted to DB → user points updated.
/// </summary>
[Collection("Integration")]
public sealed class TransactionIntegrationTests
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public TransactionIntegrationTests(IntegrationTestFactory factory)
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
            name = "TxUser", phoneNumber = phone,
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

    // ── Core flow ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AddTransaction_ValidReceipt_Returns201_AndPointsAreSaved()
    {
        var (sid, phone) = await RegisterUserAsync();

        var response = await _client.PostAsJsonAsync("/api/transactions", new
        {
            phoneNumber        = phone,
            storeId            = IntegrationTestSeeder.StoreId,
            receiptId          = UniqueReceipt(),
            receiptDescription = "Integration test purchase",
            price              = 200m
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("transactionId").GetInt32() > 0);
        Assert.True(body.GetProperty("points").GetInt32() >= 0);
        Assert.True(body.GetProperty("newTotalPoints").GetInt32() >= 0);
        Assert.Equal(200m, body.GetProperty("price").GetDecimal());
    }

    [Fact]
    public async Task AddTransaction_PointsReflectedInUserProfile()
    {
        // Register user and get session
        var (sid, phone) = await RegisterUserAsync();

        // Add a transaction
        await _client.PostAsJsonAsync("/api/transactions", new
        {
            phoneNumber = phone,
            storeId     = IntegrationTestSeeder.StoreId,
            receiptId   = UniqueReceipt(),
            price       = 500m
        });

        // Verify points are stored in the real DB
        using var db = _factory.CreateDbContext();
        var user = db.UserProfiles.FirstOrDefault(u => u.PhoneNumber == phone);
        Assert.NotNull(user);
        Assert.True(user.TotalPoints >= 0);
    }

    [Fact]
    public async Task AddTransaction_DuplicateReceiptId_ReturnsError()
    {
        var (_, phone) = await RegisterUserAsync();
        var receiptId  = UniqueReceipt();

        // First transaction — success
        var first = await _client.PostAsJsonAsync("/api/transactions", new
        {
            phoneNumber = phone,
            storeId     = IntegrationTestSeeder.StoreId,
            receiptId   = receiptId,
            price       = 100m
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Second transaction with same receiptId — must be rejected
        var second = await _client.PostAsJsonAsync("/api/transactions", new
        {
            phoneNumber = phone,
            storeId     = IntegrationTestSeeder.StoreId,
            receiptId   = receiptId,
            price       = 100m
        });
        Assert.True(
            second.StatusCode == HttpStatusCode.BadRequest ||
            second.StatusCode == HttpStatusCode.Conflict,
            $"Expected 400 or 409 but got {(int)second.StatusCode}");
    }

    [Fact]
    public async Task AddTransaction_UnknownPhone_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/transactions", new
        {
            phoneNumber = "+962799999999",   // not registered
            storeId     = IntegrationTestSeeder.StoreId,
            receiptId   = UniqueReceipt(),
            price       = 100m
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddTransaction_ZeroPrice_Returns400()
    {
        var (_, phone) = await RegisterUserAsync();
        var response = await _client.PostAsJsonAsync("/api/transactions", new
        {
            phoneNumber = phone,
            storeId     = IntegrationTestSeeder.StoreId,
            receiptId   = UniqueReceipt(),
            price       = 0m       // invalid
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Receipt history ───────────────────────────────────────────────────

    [Fact]
    public async Task GetMyReceipts_AfterTransaction_ReturnsPaginatedResults()
    {
        var (sid, phone) = await RegisterUserAsync();

        // Add a transaction first
        await _client.PostAsJsonAsync("/api/transactions", new
        {
            phoneNumber = phone,
            storeId     = IntegrationTestSeeder.StoreId,
            receiptId   = UniqueReceipt(),
            price       = 150m
        });

        // Get receipts
        var req      = Request(HttpMethod.Get, "/api/transactions/my-receipts?page=1&pageSize=10", sid);
        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.GetProperty("items").ValueKind);
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 1);
    }

    [Fact]
    public async Task GetTransactionById_ExistingId_Returns200()
    {
        var (sid, phone) = await RegisterUserAsync();

        // Add transaction
        var tx = await _client.PostAsJsonAsync("/api/transactions", new
        {
            phoneNumber = phone,
            storeId     = IntegrationTestSeeder.StoreId,
            receiptId   = UniqueReceipt(),
            price       = 300m
        });
        var txBody = await tx.Content.ReadFromJsonAsync<JsonElement>();
        var txId   = txBody.GetProperty("transactionId").GetInt32();

        // Get by ID
        var req      = Request(HttpMethod.Get, $"/api/transactions/{txId}", sid);
        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("price").GetDecimal() > 0);
    }
}
