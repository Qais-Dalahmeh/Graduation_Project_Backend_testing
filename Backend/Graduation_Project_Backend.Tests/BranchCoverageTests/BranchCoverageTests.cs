using System.Text.Json;
using Graduation_Project_Backend.DTOs.Chatbot;
using Graduation_Project_Backend.Models.Entities;
using Graduation_Project_Backend.Models.User;
using Graduation_Project_Backend.Service;
using Graduation_Project_Backend.Service.Common;
using Graduation_Project_Backend.Service.Session;
using Graduation_Project_Backend.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Graduation_Project_Backend.Tests.BranchCoverageTests;

/// <summary>
/// Non-functional branch-coverage tests.
/// Targets every boolean branch in:
///   PhoneNumberService, JsonDocumentMapper, UserAccessContext,
///   UserAccessService, SessionService, AskChatbotRequest.
/// </summary>
public sealed class BranchCoverageTests
{
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // PhoneNumberService â€” all validation branches
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    private static readonly PhoneNumberService _phone = new();

    [Fact]
    public void Normalize_NullInput_Throws()
    {
        Assert.Throws<ArgumentException>(() => _phone.Normalize(null!));
    }

    [Fact]
    public void Normalize_EmptyString_Throws()
    {
        Assert.Throws<ArgumentException>(() => _phone.Normalize(""));
    }

    [Fact]
    public void Normalize_WhitespaceOnly_Throws()
    {
        Assert.Throws<ArgumentException>(() => _phone.Normalize("   "));
    }

    [Fact]
    public void Normalize_Local07Format_ReturnsE164()
    {
        string result = _phone.Normalize("0791112233");
        Assert.Equal("+962791112233", result);
    }

    [Fact]
    public void Normalize_Local07WithDashes_NormalizesCorrectly()
    {
        string result = _phone.Normalize("079-111-2233");
        Assert.Equal("+962791112233", result);
    }

    [Fact]
    public void Normalize_Local07WithSpaces_NormalizesCorrectly()
    {
        string result = _phone.Normalize("079 111 2233");
        Assert.Equal("+962791112233", result);
    }

    [Fact]
    public void Normalize_Local07TooShort_Throws()
    {
        Assert.Throws<ArgumentException>(() => _phone.Normalize("079111"));
    }

    [Fact]
    public void Normalize_Local07TooLong_Throws()
    {
        Assert.Throws<ArgumentException>(() => _phone.Normalize("079111223344"));
    }

    [Fact]
    public void Normalize_PlusE164Valid_ReturnsAsIs()
    {
        string result = _phone.Normalize("+962791112233");
        Assert.Equal("+962791112233", result);
    }

    [Fact]
    public void Normalize_PlusNonJordanian_Throws()
    {
        Assert.Throws<ArgumentException>(() => _phone.Normalize("+1234567890123"));
    }

    [Fact]
    public void Normalize_PlusJordanianWrongLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => _phone.Normalize("+96279111"));
    }

    [Fact]
    public void Normalize_PlusJordanianNotMobile_Throws()
    {
        // 4th digit after 962 should be 7 for mobile
        Assert.Throws<ArgumentException>(() => _phone.Normalize("+962621112233"));
    }

    [Fact]
    public void Normalize_962WithoutPlus_ReturnsE164()
    {
        string result = _phone.Normalize("962791112233");
        Assert.Equal("+962791112233", result);
    }

    [Fact]
    public void Normalize_962WithoutPlusTooShort_Throws()
    {
        Assert.Throws<ArgumentException>(() => _phone.Normalize("96279111"));
    }

    [Fact]
    public void Normalize_962WithoutPlusNotMobile_Throws()
    {
        Assert.Throws<ArgumentException>(() => _phone.Normalize("962621112233"));
    }

    [Fact]
    public void Normalize_ContainsLetters_Throws()
    {
        Assert.Throws<ArgumentException>(() => _phone.Normalize("0791abc233"));
    }

    [Fact]
    public void Normalize_UnrecognizedFormat_Throws()
    {
        Assert.Throws<ArgumentException>(() => _phone.Normalize("12345678"));
    }

    [Fact]
    public void Normalize_AllPunctuationStripped_ThenInvalid_Throws()
    {
        // becomes empty after stripping
        Assert.Throws<ArgumentException>(() => _phone.Normalize("(---)"));
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // JsonDocumentMapper â€” all null / kind branches
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void ToJsonDocument_NullElement_ReturnsNull()
    {
        JsonDocument? result = JsonDocumentMapper.ToJsonDocument(null);
        Assert.Null(result);
    }

    [Fact]
    public void ToJsonDocument_JsonNull_ReturnsNull()
    {
        JsonElement nullElement = JsonDocument.Parse("null").RootElement;
        JsonDocument? result = JsonDocumentMapper.ToJsonDocument(nullElement);
        Assert.Null(result);
    }

    [Fact]
    public void ToJsonDocument_ValidObject_ReturnsDocument()
    {
        JsonElement element = JsonDocument.Parse("{\"a\":1}").RootElement;
        JsonDocument? result = JsonDocumentMapper.ToJsonDocument(element);
        Assert.NotNull(result);
        Assert.Equal(1, result!.RootElement.GetProperty("a").GetInt32());
    }

    [Fact]
    public void ToJsonDocument_ValidArray_ReturnsDocument()
    {
        JsonElement element = JsonDocument.Parse("[1,2,3]").RootElement;
        JsonDocument? result = JsonDocumentMapper.ToJsonDocument(element);
        Assert.NotNull(result);
    }

    [Fact]
    public void ToJsonElement_NullDocument_ReturnsNull()
    {
        JsonElement? result = JsonDocumentMapper.ToJsonElement(null);
        Assert.Null(result);
    }

    [Fact]
    public void ToJsonElement_ValidDocument_ReturnsElement()
    {
        using JsonDocument doc = JsonDocument.Parse("{\"x\":42}");
        JsonElement? result = JsonDocumentMapper.ToJsonElement(doc);
        Assert.NotNull(result);
        Assert.Equal(42, result!.Value.GetProperty("x").GetInt32());
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // UserAccessContext.CanAccessStore â€” 4 boolean combinations
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    private static readonly Guid _storeA = Guid.NewGuid();
    private static readonly Guid _storeB = Guid.NewGuid();

    [Fact]
    public void CanAccessStore_NotManager_ReturnsFalse()
    {
        var ctx = new UserAccessContext
        {
            IsManager = false,
            IsMallWideManager = false,
            AssignedStoreIds = [_storeA]
        };
        Assert.False(ctx.CanAccessStore(_storeA));
    }

    [Fact]
    public void CanAccessStore_MallWideManager_ReturnsTrue()
    {
        var ctx = new UserAccessContext
        {
            IsManager = true,
            IsMallWideManager = true,
            AssignedStoreIds = []
        };
        Assert.True(ctx.CanAccessStore(Guid.NewGuid()));
    }

    [Fact]
    public void CanAccessStore_StoreManagerAssignedStore_ReturnsTrue()
    {
        var ctx = new UserAccessContext
        {
            IsManager = true,
            IsMallWideManager = false,
            AssignedStoreIds = [_storeA]
        };
        Assert.True(ctx.CanAccessStore(_storeA));
    }

    [Fact]
    public void CanAccessStore_StoreManagerDifferentStore_ReturnsFalse()
    {
        var ctx = new UserAccessContext
        {
            IsManager = true,
            IsMallWideManager = false,
            AssignedStoreIds = [_storeA]
        };
        Assert.False(ctx.CanAccessStore(_storeB));
    }

    [Fact]
    public void CanAccessStore_StoreManagerEmptySet_ReturnsFalse()
    {
        var ctx = new UserAccessContext
        {
            IsManager = true,
            IsMallWideManager = false,
            AssignedStoreIds = []
        };
        Assert.False(ctx.CanAccessStore(_storeA));
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // SessionService â€” all branches
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public async Task GetSessionById_NullSessionId_ReturnsNull()
    {
        await using var db = TestInfrastructure.CreateDbContext();
        var svc = new SessionService(db);
        var result = await svc.GetSessionByIdAsync(null!);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetSessionById_EmptySessionId_ReturnsNull()
    {
        await using var db = TestInfrastructure.CreateDbContext();
        var svc = new SessionService(db);
        var result = await svc.GetSessionByIdAsync("   ");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetSessionById_NonExistentId_ReturnsNull()
    {
        await using var db = TestInfrastructure.CreateDbContext();
        var svc = new SessionService(db);
        var result = await svc.GetSessionByIdAsync("NONEXISTENT");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetSessionById_ExistingSession_ReturnsSession()
    {
        await using var db = TestInfrastructure.CreateDbContext();
        var userId = Guid.NewGuid();
        db.UserProfiles.Add(new UserProfile
        {
            Id = userId, Name = "Test", PhoneNumber = "+962791112233",
            PasswordHash = "x", Role = "user", MallID = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var svc = new SessionService(db);
        var created = await svc.CreateOrReplaceSessionAsync(userId);
        await db.SaveChangesAsync();

        var found = await svc.GetSessionByIdAsync(created.Id);
        Assert.NotNull(found);
        Assert.Equal(userId, found!.UserId);
    }

    [Fact]
    public async Task DeleteSession_EmptyId_ReturnsFalse()
    {
        await using var db = TestInfrastructure.CreateDbContext();
        var svc = new SessionService(db);
        bool result = await svc.DeleteSessionAsync("");
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteSession_WhitespaceId_ReturnsFalse()
    {
        await using var db = TestInfrastructure.CreateDbContext();
        var svc = new SessionService(db);
        bool result = await svc.DeleteSessionAsync("   ");
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteSession_NonExistentId_ReturnsFalse()
    {
        await using var db = TestInfrastructure.CreateDbContext();
        var svc = new SessionService(db);
        bool result = await svc.DeleteSessionAsync("DOESNOTEXIST");
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteSession_ExistingSession_ReturnsTrueAndRemoves()
    {
        await using var db = TestInfrastructure.CreateDbContext();
        var userId = Guid.NewGuid();
        db.UserProfiles.Add(new UserProfile
        {
            Id = userId, Name = "Del", PhoneNumber = "+962791112234",
            PasswordHash = "x", Role = "user", MallID = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var svc = new SessionService(db);
        var session = await svc.CreateOrReplaceSessionAsync(userId);
        await db.SaveChangesAsync();

        bool deleted = await svc.DeleteSessionAsync(session.Id);
        Assert.True(deleted);
        Assert.Null(await svc.GetSessionByIdAsync(session.Id));
    }

    [Fact]
    public async Task CreateOrReplaceSession_ExistingSession_ReplacesIt()
    {
        await using var db = TestInfrastructure.CreateDbContext();
        var userId = Guid.NewGuid();
        db.UserProfiles.Add(new UserProfile
        {
            Id = userId, Name = "Rep", PhoneNumber = "+962791112235",
            PasswordHash = "x", Role = "user", MallID = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var svc = new SessionService(db);
        var first = await svc.CreateOrReplaceSessionAsync(userId);
        await db.SaveChangesAsync();

        var second = await svc.CreateOrReplaceSessionAsync(userId);
        await db.SaveChangesAsync();

        // old session should be gone
        Assert.Null(await svc.GetSessionByIdAsync(first.Id));
        // new session should exist
        Assert.NotNull(await svc.GetSessionByIdAsync(second.Id));
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // UserAccessService â€” manager/non-manager, mall mismatch branches
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public async Task GetUserAccessContext_NonManagerUser_IsManagerFalse()
    {
        await using var db = TestInfrastructure.CreateDbContext();
        var mallId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.UserProfiles.Add(new UserProfile
        {
            Id = userId, Name = "Regular", PhoneNumber = "+962791112236",
            PasswordHash = "x", Role = "user", MallID = mallId
        });
        await db.SaveChangesAsync();

        var svc = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        var ctx = await svc.GetUserAccessContextAsync(userId);

        Assert.False(ctx.IsManager);
        Assert.Equal(mallId, ctx.MallID);
        Assert.Null(ctx.ManagerId);
    }

    [Fact]
    public async Task GetUserAccessContext_ManagerWithNoStores_IsMallWideTrue()
    {
        await using var db = TestInfrastructure.CreateDbContext();
        var mallId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.UserProfiles.Add(new UserProfile
        {
            Id = userId, Name = "Mgr", PhoneNumber = "+962791112237",
            PasswordHash = "x", Role = "manager", MallID = mallId
        });
        db.Managers.Add(new Manager
        {
            Id = userId, Name = "Mgr", MallID = mallId, Role = "manager"
        });
        await db.SaveChangesAsync();

        var svc = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        var ctx = await svc.GetUserAccessContextAsync(userId);

        Assert.True(ctx.IsManager);
        Assert.True(ctx.IsMallWideManager);
        Assert.Empty(ctx.AssignedStoreIds);
    }

    [Fact]
    public async Task GetUserAccessContext_ManagerWithAssignedStores_IsMallWideFalse()
    {
        await using var db = TestInfrastructure.CreateDbContext();
        var mallId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile
        {
            Id = userId, Name = "Mgr2", PhoneNumber = "+962791112238",
            PasswordHash = "x", Role = "manager", MallID = mallId
        });
        db.Managers.Add(new Manager
        {
            Id = userId, Name = "Mgr2", MallID = mallId, Role = "store_manager"
        });
        db.Stores.Add(new Store
        {
            Id = storeId, Name = "Store A", MallID = mallId
        });
        db.Management.Add(new Management
        {
            ManagerId = userId, StoreId = storeId
        });
        await db.SaveChangesAsync();

        var svc = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        var ctx = await svc.GetUserAccessContextAsync(userId);

        Assert.True(ctx.IsManager);
        Assert.False(ctx.IsMallWideManager);
        Assert.Contains(storeId, ctx.AssignedStoreIds);
    }

    [Fact]
    public async Task GetUserAccessContext_ManagerMallMismatch_LogsWarning()
    {
        await using var db = TestInfrastructure.CreateDbContext();
        var userMallId = Guid.NewGuid();
        var mgrMallId  = Guid.NewGuid(); // different mall
        var userId     = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile
        {
            Id = userId, Name = "MismatchMgr", PhoneNumber = "+962791112239",
            PasswordHash = "x", Role = "manager", MallID = userMallId
        });
        db.Managers.Add(new Manager
        {
            Id = userId, Name = "MismatchMgr", MallID = mgrMallId, Role = "manager"
        });
        await db.SaveChangesAsync();

        var svc = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        var ctx = await svc.GetUserAccessContextAsync(userId);

        // Should use manager's mall, not user's mall
        Assert.Equal(mgrMallId, ctx.MallID);
    }

    [Fact]
    public async Task GetUserAccessContext_NonExistentUser_Throws()
    {
        await using var db = TestInfrastructure.CreateDbContext();
        var svc = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        await Assert.ThrowsAsync<Graduation_Project_Backend.Service.Common.ApiUnauthorizedException>(
            () => svc.GetUserAccessContextAsync(Guid.NewGuid()));
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // AskChatbotRequest.GetMessage() â€” all field name aliases
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void AskChatbotRequest_GetMessage_ViaMsg()
    {
        var req = new AskChatbotRequest { Msg = "hello via msg" };
        Assert.Equal("hello via msg", req.GetMessage());
    }

    [Fact]
    public void AskChatbotRequest_GetMessage_ViaMessage()
    {
        var req = new AskChatbotRequest { Message = "hello via message" };
        Assert.Equal("hello via message", req.GetMessage());
    }

    [Fact]
    public void AskChatbotRequest_GetMessage_ViaMessege()
    {
        var req = new AskChatbotRequest { Messege = "hello via messege" };
        Assert.Equal("hello via messege", req.GetMessage());
    }

    [Fact]
    public void AskChatbotRequest_GetMessage_ViaText()
    {
        var req = new AskChatbotRequest { Text = "hello via text" };
        Assert.Equal("hello via text", req.GetMessage());
    }

    [Fact]
    public void AskChatbotRequest_GetMessage_ViaQuestion()
    {
        var req = new AskChatbotRequest { Question = "hello via question" };
        Assert.Equal("hello via question", req.GetMessage());
    }

    [Fact]
    public void AskChatbotRequest_GetMessage_AllNull_ReturnsNull()
    {
        var req = new AskChatbotRequest();
        Assert.Null(req.GetMessage());
    }

    [Fact]
    public void AskChatbotRequest_GetMessage_MsgTakesPriority()
    {
        var req = new AskChatbotRequest
        {
            Msg = "from msg",
            Message = "from message",
            Text = "from text"
        };
        Assert.Equal("from msg", req.GetMessage());
    }

    [Fact]
    public void AskChatbotRequest_GetMessage_ViaParsedJson_MsgField()
    {
        var json = """{"msg":"from extra msg"}""";
        var req = JsonSerializer.Deserialize<AskChatbotRequest>(json)!;
        Assert.Equal("from extra msg", req.GetMessage());
    }

    [Fact]
    public void AskChatbotRequest_GetMessage_ViaParsedJson_MessageField()
    {
        var json = """{"message":"from extra message"}""";
        var req = JsonSerializer.Deserialize<AskChatbotRequest>(json)!;
        Assert.Equal("from extra message", req.GetMessage());
    }

    [Fact]
    public void AskChatbotRequest_GetMessage_ViaParsedJson_QuestionField()
    {
        var json = """{"question":"what time does the mall close?"}""";
        var req = JsonSerializer.Deserialize<AskChatbotRequest>(json)!;
        Assert.Equal("what time does the mall close?", req.GetMessage());
    }

    [Fact]
    public void AskChatbotRequest_ConversationSessionId_IsSet()
    {
        var id  = Guid.NewGuid();
        var req = new AskChatbotRequest { ConversationSessionId = id };
        Assert.Equal(id, req.ConversationSessionId);
    }
}

