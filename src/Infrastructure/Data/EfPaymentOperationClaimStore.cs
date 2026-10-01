using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentOperationClaimAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.Infrastructure.Data;

/// <summary>
/// EF-backed claim store. A second claim for the same key is refused by the primary-key conflict on
/// insert - confirmed empirically against EF Core's InMemory provider: a duplicate key across two
/// DbContext instances (the realistic case - two requests, two scoped contexts) throws
/// <see cref="ArgumentException"/>; within a single context it throws <see cref="InvalidOperationException"/>.
/// A real relational provider (SQL Server) throws <see cref="DbUpdateException"/> from the unique index.
/// All three are caught here.
/// </summary>
public class EfPaymentOperationClaimStore : IPaymentOperationClaimStore
{
    private readonly CatalogContext _dbContext;

    public EfPaymentOperationClaimStore(CatalogContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> TryClaimAsync(string claimKey, CancellationToken cancellationToken)
    {
        var claim = new PaymentOperationClaim(claimKey);
        _dbContext.Add(claim);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException)
        {
            _dbContext.Entry(claim).State = EntityState.Detached;
            return false;
        }
    }
}
