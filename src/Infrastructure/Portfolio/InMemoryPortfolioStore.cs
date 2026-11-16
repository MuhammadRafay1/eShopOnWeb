using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio;

/// <summary>
/// Thread-safe, in-process implementation of <see cref="IPortfolioStore"/>. Registered as a
/// singleton so state survives for the lifetime of the host process (matching the environment's
/// in-memory database model). Holds only Upvest id references and operator model definitions —
/// no shopper personal data.
/// </summary>
public sealed class InMemoryPortfolioStore : IPortfolioStore
{
    private readonly ConcurrentDictionary<string, InvestorRecord> _investors = new();
    private readonly ConcurrentDictionary<string, ModelPortfolio> _models = new();
    private readonly ConcurrentDictionary<string, List<PaymentRecord>> _payments = new();
    private readonly ConcurrentDictionary<string, RealignmentRecord> _realignments = new();

    public InvestorRecord? GetInvestor(string shopperId)
        => _investors.TryGetValue(shopperId, out var investor) ? investor : null;

    public void SaveInvestor(InvestorRecord investor)
        => _investors[investor.ShopperId] = investor;

    public ModelPortfolio? GetModel(string modelId)
        => _models.TryGetValue(modelId, out var model) ? model : null;

    public IReadOnlyList<ModelPortfolio> GetModels()
        => _models.Values.OrderBy(m => m.ModelId, System.StringComparer.Ordinal).ToList();

    public void SaveModel(ModelPortfolio model)
        => _models[model.ModelId] = model;

    public void AddPayment(PaymentRecord payment)
    {
        var list = _payments.GetOrAdd(payment.ShopperId, _ => new List<PaymentRecord>());
        lock (list)
        {
            list.Add(payment);
        }
    }

    public IReadOnlyList<PaymentRecord> GetPayments(string shopperId)
    {
        if (!_payments.TryGetValue(shopperId, out var list)) return new List<PaymentRecord>();
        lock (list)
        {
            // Newest first.
            return list.OrderByDescending(p => p.CreatedAt).ToList();
        }
    }

    public PaymentRecord? GetPayment(string shopperId, string paymentId)
    {
        if (!_payments.TryGetValue(shopperId, out var list)) return null;
        lock (list)
        {
            return list.FirstOrDefault(p => p.PaymentId == paymentId);
        }
    }

    public void AddRealignment(RealignmentRecord realignment)
        => _realignments[realignment.RealignmentId] = realignment;

    public RealignmentRecord? GetRealignment(string realignmentId)
        => _realignments.TryGetValue(realignmentId, out var r) ? r : null;
}
