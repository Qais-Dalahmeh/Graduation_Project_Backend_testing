using Graduation_Project_Backend.DTOs.Stores;
using Graduation_Project_Backend.Models.Entities;
using Graduation_Project_Backend.Models.User;
using Graduation_Project_Backend.Service;
using Graduation_Project_Backend.Service.Common;
using Graduation_Project_Backend.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Graduation_Project_Backend.Tests.BranchCoverageTests;

/// <summary>
/// Coverage-gap tests for StoresService methods not yet reached.
/// Covers: GetStoresAsync, GetStoreByIdAsync, GetVisibleStoresAsync,
///         GetVisibleStoreByIdAsync, GetManagedStoresAsync, UpdateStoreAsync.
/// </summary>
public sealed class CoverageGapTests_StoresService
{
    private static StoresService CreateService(AppDbContext db)
    {
        var access = new UserAccessService(db, NullLogger<UserAccessService>.Instance);
        return new StoresService(db, access, NullLogger<StoresService>.Instance);
    }

    /// <summary>Sets up a mall-wide manager with one store and one category.</summary>
    private static (AppDbContext db, Guid managerId, Guid mallId, Guid storeId, long catId) SetupMallWideManager()
    {
        AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();
        Guid storeId = Guid.NewGuid();
        long catId = 10;
        DateTimeOffset now = DateTimeOffset.UtcNow;

        db.Malls.Add(new Mall { Id = mallId, Name = "Test Mall", CreatedAt = now });
        db.UserProfiles.Add(new UserProfile { Id = managerId, Name = "Mgr", PhoneNumber = "+962700600001", PasswordHash = "h", Role = "manager", MallID = mallId });
        db.Managers.Add(new Manager { Id = managerId, Name = "Mgr", Role = "manager", MallID = mallId });
        db.Stores.Add(new Store { Id = storeId, Name = "Zara", MallID = mallId });
        db.Categories.Add(new Category { Id = catId, Name = "Clothing", MallID = mallId });
        db.StoreCategories.Add(new StoreCategory { StoreId = storeId, CategoryId = catId });
        db.SaveChanges();

        return (db, managerId, mallId, storeId, catId);
    }

    private static (AppDbContext db, Guid userId, Guid mallId, Guid storeId) SetupRegularUser()
    {
        AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        Guid storeId = Guid.NewGuid();

        db.Malls.Add(new Mall { Id = mallId, Name = "Test Mall", CreatedAt = DateTimeOffset.UtcNow });
        db.UserProfiles.Add(new UserProfile { Id = userId, Name = "User", PhoneNumber = "+962700600002", PasswordHash = "h", Role = "user", MallID = mallId });
        db.Stores.Add(new Store { Id = storeId, Name = "H&M", MallID = mallId });
        db.SaveChanges();

        return (db, userId, mallId, storeId);
    }

    // â”€â”€ GetStoresAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task GetStoresAsync_ReturnsAllStores_OrderedByName()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = DateTimeOffset.UtcNow });
        db.Stores.AddRange(
            new Store { Id = Guid.NewGuid(), Name = "Zara", MallID = mallId },
            new Store { Id = Guid.NewGuid(), Name = "Apple", MallID = mallId });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetStoresAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("Apple", result[0].Name);
        Assert.Equal("Zara", result[1].Name);
    }

    [Fact]
    public async Task GetStoresAsync_ReturnsEmpty_WhenNoStores()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        var service = CreateService(db);
        var result = await service.GetStoresAsync();
        Assert.Empty(result);
    }

    // â”€â”€ GetStoreByIdAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task GetStoreByIdAsync_ReturnsNull_WhenNotFound()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        var result = await CreateService(db).GetStoreByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetStoreByIdAsync_ReturnsStore_WhenFound()
    {
        using AppDbContext db = TestInfrastructure.CreateDbContext();
        Guid mallId = Guid.NewGuid();
        Guid storeId = Guid.NewGuid();

        db.Malls.Add(new Mall { Id = mallId, Name = "Mall", CreatedAt = DateTimeOffset.UtcNow });
        db.Stores.Add(new Store { Id = storeId, Name = "Nike", MallID = mallId });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetStoreByIdAsync(storeId);

        Assert.NotNull(result);
        Assert.Equal("Nike", result.Name);
    }

    // â”€â”€ GetVisibleStoresAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task GetVisibleStoresAsync_ReturnsStoresForUserMall()
    {
        var (db, userId, mallId, storeId) = SetupRegularUser();
        using (db)
        {
            // Add a store in a different mall â€” should not appear
            Guid otherMallId = Guid.NewGuid();
            db.Malls.Add(new Mall { Id = otherMallId, Name = "Other Mall", CreatedAt = DateTimeOffset.UtcNow });
            db.Stores.Add(new Store { Id = Guid.NewGuid(), Name = "Gucci", MallID = otherMallId });
            await db.SaveChangesAsync();

            var service = CreateService(db);
            var result = await service.GetVisibleStoresAsync(userId);

            Assert.Single(result);
            Assert.Equal("H&M", result[0].Name);
        }
    }

    // â”€â”€ GetVisibleStoreByIdAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task GetVisibleStoreByIdAsync_ReturnsNull_WhenStoreNotInUserMall()
    {
        var (db, userId, _, _) = SetupRegularUser();
        using (db)
        {
            var service = CreateService(db);
            var result = await service.GetVisibleStoreByIdAsync(userId, Guid.NewGuid());
            Assert.Null(result);
        }
    }

    [Fact]
    public async Task GetVisibleStoreByIdAsync_ReturnsStore_WhenInUserMall()
    {
        var (db, userId, _, storeId) = SetupRegularUser();
        using (db)
        {
            var service = CreateService(db);
            var result = await service.GetVisibleStoreByIdAsync(userId, storeId);

            Assert.NotNull(result);
            Assert.Equal("H&M", result.Name);
        }
    }

    // â”€â”€ GetManagedStoresAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task GetManagedStoresAsync_ReturnsMallStores_WithCategories()
    {
        var (db, managerId, _, storeId, _) = SetupMallWideManager();
        using (db)
        {
            var service = CreateService(db);
            var result = await service.GetManagedStoresAsync(managerId);

            Assert.Single(result);
            Assert.Equal("Zara", result[0].Name);
            Assert.Contains("Clothing", result[0].Categories);
        }
    }

    [Fact]
    public async Task GetManagedStoresAsync_ThrowsForbidden_ForRegularUser()
    {
        var (db, userId, _, _) = SetupRegularUser();
        using (db)
        {
            var service = CreateService(db);
            await Assert.ThrowsAsync<ApiForbiddenException>(() =>
                service.GetManagedStoresAsync(userId));
        }
    }

    // â”€â”€ UpdateStoreAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task UpdateStoreAsync_UpdatesNameAndPersists()
    {
        var (db, managerId, mallId, storeId, catId) = SetupMallWideManager();
        using (db)
        {
            var service = CreateService(db);
            var request = new UpdateStoreRequest
            {
                Name = "Zara Updated",
                CategoryIds = new List<long> { catId }
            };

            var result = await service.UpdateStoreAsync(managerId, storeId, request);

            Assert.Equal("Zara Updated", result.Name);
        }
    }

    [Fact]
    public async Task UpdateStoreAsync_ThrowsNotFound_WhenStoreNotInMall()
    {
        var (db, managerId, _, _, _) = SetupMallWideManager();
        using (db)
        {
            var service = CreateService(db);
            var request = new UpdateStoreRequest { Name = "Ghost Store" };

            await Assert.ThrowsAsync<ApiNotFoundException>(() =>
                service.UpdateStoreAsync(managerId, Guid.NewGuid(), request));
        }
    }

    [Fact]
    public async Task UpdateStoreAsync_ClearsAndReSyncsCategories()
    {
        var (db, managerId, mallId, storeId, catId) = SetupMallWideManager();
        using (db)
        {
            long newCatId = 11;
            db.Categories.Add(new Category { Id = newCatId, Name = "Shoes", MallID = mallId });
            await db.SaveChangesAsync();

            var service = CreateService(db);
            var request = new UpdateStoreRequest
            {
                Name = "Zara",
                CategoryIds = new List<long> { newCatId }
            };

            var result = await service.UpdateStoreAsync(managerId, storeId, request);

            Assert.Single(result.Categories);
            Assert.Equal("Shoes", result.Categories[0].Name);
        }
    }
}

