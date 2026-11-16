using System;
using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Application-owned store for the portfolio feature, keyed by shopper. The contract guarantees atomic
/// insert-or-fail claims (<see cref="TryCreateInvestor"/>, <see cref="TryClaimPayment"/>) so a duplicate
/// request is refused by the store rather than reaching Upvest twice. One investor per <c>buyerId</c>; a
/// shopper only ever sees their own investor, payments and portfolio.
/// </summary>
public interface IPortfolioStore
{
    // ---- Investor (one per shopper; buyerId is the key) ----

    /// <summary>Atomically insert a new investor. Returns false if one already exists for the buyerId.</summary>
    bool TryCreateInvestor(Investor investor);

    Investor? GetInvestor(string buyerId);

    /// <summary>Persist mutations to an already-stored investor.</summary>
    void UpdateInvestor(Investor investor);

    /// <summary>Roll back a claimed investor (used when onboarding fails definitively before any Upvest write landed).</summary>
    void RemoveInvestor(string buyerId);

    // ---- Model catalog (operator-owned; keyed by the route modelId) ----

    void UpsertModel(ModelPortfolio model);
    ModelPortfolio? GetModel(string modelId);
    IReadOnlyList<ModelPortfolio> ListModels();

    // ---- Payments (per shopper) ----

    /// <summary>
    /// Atomically claim a pay-in for (buyerId, idempotencyKey). Returns false and the existing payment when
    /// that key was already used by this shopper, so a retried pay-in is not placed twice.
    /// </summary>
    bool TryClaimPayment(InvestorPayment payment, out InvestorPayment existing);

    void UpdatePayment(InvestorPayment payment);

    /// <summary>Release a claimed payment (used when the Upvest order could not be placed).</summary>
    void RemovePayment(string buyerId, Guid paymentId);

    /// <summary>The shopper's payments, newest first.</summary>
    IReadOnlyList<InvestorPayment> ListPayments(string buyerId);

    InvestorPayment? GetPayment(string buyerId, Guid paymentId);

    // ---- Realignments (operator-triggered; keyed by Upvest execution id) ----

    void SaveRealignment(Realignment realignment);
    Realignment? GetRealignment(Guid realignmentId);
}
