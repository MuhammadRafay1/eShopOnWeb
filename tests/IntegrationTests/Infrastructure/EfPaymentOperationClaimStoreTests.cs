using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.Infrastructure.Data;
using Xunit;

namespace Microsoft.eShopWeb.IntegrationTests.Infrastructure;

public class EfPaymentOperationClaimStoreTests
{
    private static DbContextOptions<CatalogContext> NewInMemoryOptions(string dbName) =>
        new DbContextOptionsBuilder<CatalogContext>().UseInMemoryDatabase(dbName).Options;

    [Fact]
    public async Task TryClaimAsync_FirstCaller_Wins()
    {
        var options = NewInMemoryOptions(Guid.NewGuid().ToString());
        await using var dbContext = new CatalogContext(options);
        var store = new EfPaymentOperationClaimStore(dbContext);

        var claimed = await store.TryClaimAsync("1:authorize", CancellationToken.None);

        Assert.True(claimed);
    }

    [Fact]
    public async Task TryClaimAsync_SecondCaller_SameKey_AcrossSeparateScopedContexts_IsRefused()
    {
        // Two separate DbContext instances against the same in-memory database is the realistic shape:
        // two HTTP requests, two DI scopes, same underlying store.
        var dbName = Guid.NewGuid().ToString();

        await using (var first = new CatalogContext(NewInMemoryOptions(dbName)))
        {
            var firstStore = new EfPaymentOperationClaimStore(first);
            Assert.True(await firstStore.TryClaimAsync("1:capture", CancellationToken.None));
        }

        await using (var second = new CatalogContext(NewInMemoryOptions(dbName)))
        {
            var secondStore = new EfPaymentOperationClaimStore(second);
            Assert.False(await secondStore.TryClaimAsync("1:capture", CancellationToken.None));
        }
    }

    [Fact]
    public async Task TryClaimAsync_DistinctKeys_BothSucceed()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var first = new CatalogContext(NewInMemoryOptions(dbName)))
        {
            Assert.True(await new EfPaymentOperationClaimStore(first).TryClaimAsync("1:refund:key-a", CancellationToken.None));
        }

        await using (var second = new CatalogContext(NewInMemoryOptions(dbName)))
        {
            Assert.True(await new EfPaymentOperationClaimStore(second).TryClaimAsync("1:refund:key-b", CancellationToken.None));
        }
    }

    [Fact]
    public async Task TryClaimAsync_AfterRefusal_ContextCanStillBeUsedForOtherWrites()
    {
        // The failed claim attempt must not leave the DbContext's change tracker poisoned for the rest
        // of the (scoped, reused) request.
        var dbName = Guid.NewGuid().ToString();
        await using var seed = new CatalogContext(NewInMemoryOptions(dbName));
        await new EfPaymentOperationClaimStore(seed).TryClaimAsync("1:void", CancellationToken.None);

        await using var dbContext = new CatalogContext(NewInMemoryOptions(dbName));
        var store = new EfPaymentOperationClaimStore(dbContext);

        var refused = await store.TryClaimAsync("1:void", CancellationToken.None);
        Assert.False(refused);

        // The same context can still successfully claim a different key afterwards.
        var claimed = await store.TryClaimAsync("1:void:retry", CancellationToken.None);
        Assert.True(claimed);
    }
}
