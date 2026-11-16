using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio;

/// <summary>
/// Thread-safe, in-memory implementation of <see cref="IPortfolioStore"/>, registered as a singleton.
/// This matches the environment's "in-memory only, per-host" constraint: data survives one run. The
/// interface keeps this swappable for a durable store without touching the web or gateway layers.
/// Atomicity of the claim operations (<see cref="TryCreateInvestor"/>, <see cref="TryClaimPayment"/>) is
/// provided by <see cref="ConcurrentDictionary{TKey,TValue}.TryAdd"/>, which rejects the second caller.
/// </summary>
public sealed class InMemoryPortfolioStore : IPortfolioStore
{
    private readonly ConcurrentDictionary<string, Investor> _investors = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ModelPortfolio> _models = new(StringComparer.Ordinal);

    // Per-shopper payment claims, keyed by "buyerId|idempotencyKey", and per-shopper payment lists.
    private readonly ConcurrentDictionary<string, InvestorPayment> _paymentClaims = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, InvestorPayment>> _payments = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<Guid, Realignment> _realignments = new();

    private static string PaymentClaimKey(string buyerId, Guid idempotencyKey) => $"{buyerId}|{idempotencyKey:N}";

    // ---- Investor ----

    public bool TryCreateInvestor(Investor investor) => _investors.TryAdd(investor.BuyerId, investor);

    public Investor? GetInvestor(string buyerId) => _investors.TryGetValue(buyerId, out var v) ? v : null;

    public void UpdateInvestor(Investor investor) => _investors[investor.BuyerId] = investor;

    public void RemoveInvestor(string buyerId) => _investors.TryRemove(buyerId, out _);

    // ---- Models ----

    public void UpsertModel(ModelPortfolio model) => _models[model.ModelId] = model;

    public ModelPortfolio? GetModel(string modelId) => _models.TryGetValue(modelId, out var v) ? v : null;

    public IReadOnlyList<ModelPortfolio> ListModels() => _models.Values.OrderBy(m => m.CreatedAt).ToList();

    // ---- Payments ----

    public bool TryClaimPayment(InvestorPayment payment, out InvestorPayment existing)
    {
        var key = PaymentClaimKey(payment.BuyerId, payment.IdempotencyKey);
        if (!_paymentClaims.TryAdd(key, payment))
        {
            existing = _paymentClaims[key];
            return false;
        }

        var bucket = _payments.GetOrAdd(payment.BuyerId, _ => new ConcurrentDictionary<Guid, InvestorPayment>());
        bucket[payment.Id] = payment;
        existing = payment;
        return true;
    }

    public void UpdatePayment(InvestorPayment payment)
    {
        var bucket = _payments.GetOrAdd(payment.BuyerId, _ => new ConcurrentDictionary<Guid, InvestorPayment>());
        bucket[payment.Id] = payment;
    }

    public void RemovePayment(string buyerId, Guid paymentId)
    {
        if (_payments.TryGetValue(buyerId, out var bucket) && bucket.TryRemove(paymentId, out var removed))
        {
            _paymentClaims.TryRemove(PaymentClaimKey(buyerId, removed.IdempotencyKey), out _);
        }
    }

    public IReadOnlyList<InvestorPayment> ListPayments(string buyerId) =>
        _payments.TryGetValue(buyerId, out var bucket)
            ? bucket.Values.OrderByDescending(p => p.CreatedAt).ToList()
            : new List<InvestorPayment>();

    public InvestorPayment? GetPayment(string buyerId, Guid paymentId) =>
        _payments.TryGetValue(buyerId, out var bucket) && bucket.TryGetValue(paymentId, out var p) ? p : null;

    // ---- Realignments ----

    public void SaveRealignment(Realignment realignment) => _realignments[realignment.Id] = realignment;

    public Realignment? GetRealignment(Guid realignmentId) =>
        _realignments.TryGetValue(realignmentId, out var v) ? v : null;
}
