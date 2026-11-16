using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;
using Microsoft.eShopWeb.Infrastructure.Data;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio;

/// <summary>
/// EF Core persistence for portfolio state. Claim inserts rely on the primary key: a second claim for an
/// existing key fails at <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> and is reported as a
/// rejection (false), never silently overwriting the first.
/// </summary>
public class EfPortfolioRepository : IPortfolioRepository
{
    private readonly PortfolioContext _db;

    public EfPortfolioRepository(PortfolioContext db) => _db = db;

    // ---- Investors ----

    public Task<InvestorRecord?> GetInvestorAsync(string shopper, CancellationToken cancellationToken) =>
        _db.Investors.FirstOrDefaultAsync(i => i.Shopper == shopper, cancellationToken);

    public async Task<bool> TryClaimInvestorAsync(InvestorRecord investor, CancellationToken cancellationToken)
    {
        if (await _db.Investors.AnyAsync(i => i.Shopper == investor.Shopper, cancellationToken))
            return false;

        _db.Investors.Add(investor);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex) when (IsDuplicateKey(ex))
        {
            _db.Entry(investor).State = EntityState.Detached;
            return false;
        }
    }

    public async Task UpdateInvestorAsync(InvestorRecord investor, CancellationToken cancellationToken)
    {
        investor.UpdatedAt = DateTimeOffset.UtcNow;
        _db.Investors.Update(investor);
        await _db.SaveChangesAsync(cancellationToken);
    }

    // ---- Models ----

    public Task<ModelPortfolioRecord?> GetModelAsync(string modelId, CancellationToken cancellationToken) =>
        _db.Models.Include(m => m.Funds).FirstOrDefaultAsync(m => m.ModelId == modelId, cancellationToken);

    public async Task<IReadOnlyList<ModelPortfolioRecord>> ListModelsAsync(CancellationToken cancellationToken) =>
        await _db.Models.Include(m => m.Funds).AsNoTracking().ToListAsync(cancellationToken);

    public async Task UpsertModelAsync(ModelPortfolioRecord model, CancellationToken cancellationToken)
    {
        var existing = await _db.Models.Include(m => m.Funds)
            .FirstOrDefaultAsync(m => m.ModelId == model.ModelId, cancellationToken);

        if (existing is null)
        {
            _db.Models.Add(model);
        }
        else
        {
            existing.Name = model.Name;
            existing.AllocationId = model.AllocationId;
            existing.UpdatedAt = model.UpdatedAt;
            _db.ModelFunds.RemoveRange(existing.Funds);
            existing.Funds = model.Funds;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    // ---- Payments ----

    public Task<PaymentRecord?> GetPaymentAsync(Guid paymentId, CancellationToken cancellationToken) =>
        _db.Payments.FirstOrDefaultAsync(p => p.PaymentId == paymentId, cancellationToken);

    public async Task<bool> TryClaimPaymentAsync(PaymentRecord payment, CancellationToken cancellationToken)
    {
        if (await _db.Payments.AnyAsync(p => p.PaymentId == payment.PaymentId, cancellationToken))
            return false;

        _db.Payments.Add(payment);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex) when (IsDuplicateKey(ex))
        {
            _db.Entry(payment).State = EntityState.Detached;
            return false;
        }
    }

    public async Task UpdatePaymentAsync(PaymentRecord payment, CancellationToken cancellationToken)
    {
        _db.Payments.Update(payment);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentRecord>> ListPaymentsAsync(string shopper, CancellationToken cancellationToken) =>
        await _db.Payments.AsNoTracking()
            .Where(p => p.Shopper == shopper)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

    // ---- Realignments ----

    public Task<RealignmentRecord?> GetRealignmentAsync(Guid realignmentId, CancellationToken cancellationToken) =>
        _db.Realignments.FirstOrDefaultAsync(r => r.RealignmentId == realignmentId, cancellationToken);

    public async Task AddRealignmentAsync(RealignmentRecord realignment, CancellationToken cancellationToken)
    {
        _db.Realignments.Add(realignment);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// A primary-key collision on insert. SQL Server surfaces it as <see cref="DbUpdateException"/>; the
    /// in-memory provider as <see cref="ArgumentException"/> ("An item with the same key has already been
    /// added."). Both mean the claim was already taken.
    /// </summary>
    private static bool IsDuplicateKey(Exception ex) =>
        ex is DbUpdateException || ex is ArgumentException || ex is InvalidOperationException;
}
