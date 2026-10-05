using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.Infrastructure.Data;

/// <summary>
/// EF-backed claims. A claim is an INSERT keyed by the row's primary key, so the database (or the in-memory store)
/// refuses the second claim for the same key — no read-then-insert window. Entities are detached after every
/// operation so a later claim in the same request never collides with a tracked instance.
/// </summary>
public class SubscriptionClaimStore : ISubscriptionClaimStore
{
    private readonly CatalogContext _db;

    public SubscriptionClaimStore(CatalogContext db)
    {
        _db = db;
    }

    public Task<BillingCustomer?> GetCustomerAsync(string buyerId, CancellationToken cancellationToken = default) =>
        _db.BillingCustomers.AsNoTracking().FirstOrDefaultAsync(c => c.BuyerId == buyerId, cancellationToken);

    public Task<bool> TryClaimCustomerAsync(BillingCustomer customer, CancellationToken cancellationToken = default) =>
        TryInsertAsync(customer, () => _db.BillingCustomers.AsNoTracking().AnyAsync(c => c.BuyerId == customer.BuyerId), cancellationToken);

    public Task SaveCustomerAsync(BillingCustomer customer, CancellationToken cancellationToken = default) =>
        UpdateAsync(customer, cancellationToken);

    public async Task ReleaseCustomerAsync(BillingCustomer customer, CancellationToken cancellationToken = default) =>
        await DeleteAsync(customer, cancellationToken);

    public Task<SubscriptionEnrollment?> GetEnrollmentAsync(string buyerId, string planHandle, CancellationToken cancellationToken = default) =>
        _db.SubscriptionEnrollments.AsNoTracking()
            .FirstOrDefaultAsync(e => e.BuyerId == buyerId && e.PlanHandle == planHandle, cancellationToken);

    public async Task<IReadOnlyList<SubscriptionEnrollment>> ListEnrollmentsAsync(string buyerId, CancellationToken cancellationToken = default) =>
        await _db.SubscriptionEnrollments.AsNoTracking().Where(e => e.BuyerId == buyerId).ToListAsync(cancellationToken);

    public Task<bool> TryClaimEnrollmentAsync(SubscriptionEnrollment enrollment, CancellationToken cancellationToken = default) =>
        TryInsertAsync(enrollment,
            () => _db.SubscriptionEnrollments.AsNoTracking()
                .AnyAsync(e => e.BuyerId == enrollment.BuyerId && e.PlanHandle == enrollment.PlanHandle),
            cancellationToken);

    public Task SaveEnrollmentAsync(SubscriptionEnrollment enrollment, CancellationToken cancellationToken = default) =>
        UpdateAsync(enrollment, cancellationToken);

    public Task<bool> ReleaseEnrollmentAsync(SubscriptionEnrollment enrollment, CancellationToken cancellationToken = default) =>
        DeleteAsync(enrollment, cancellationToken);

    private async Task<bool> TryInsertAsync<T>(T entity, Func<Task<bool>> keyIsTaken, CancellationToken cancellationToken)
        where T : class
    {
        _db.Add(entity);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is DbUpdateException or ArgumentException or InvalidOperationException)
        {
            // SQL Server raises DbUpdateException for the key violation; the in-memory provider raises
            // ArgumentException. Only treat it as a refused claim when the key really is taken.
            _db.Entry(entity).State = EntityState.Detached;
            if (await keyIsTaken())
            {
                return false;
            }
            throw;
        }
        finally
        {
            _db.Entry(entity).State = EntityState.Detached;
        }
    }

    private async Task UpdateAsync<T>(T entity, CancellationToken cancellationToken) where T : class
    {
        _db.Update(entity);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _db.Entry(entity).State = EntityState.Detached;
        }
    }

    private async Task<bool> DeleteAsync<T>(T entity, CancellationToken cancellationToken) where T : class
    {
        _db.Remove(entity);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false; // already released by another request
        }
        finally
        {
            _db.Entry(entity).State = EntityState.Detached;
        }
    }
}
