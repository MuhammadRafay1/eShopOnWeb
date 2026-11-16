using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Local persistence for the portfolio feature. Holds only cross-cutting references
/// (shopper-to-Upvest id mappings, operator model definitions, payment and realignment
/// references) — never shopper personal details. The source of truth for live status and
/// holdings is always Upvest.
/// </summary>
public interface IPortfolioStore
{
    // Investors (scoped per shopper)
    InvestorRecord? GetInvestor(string shopperId);
    void SaveInvestor(InvestorRecord investor);

    // Model portfolios (global / operator-owned)
    ModelPortfolio? GetModel(string modelId);
    IReadOnlyList<ModelPortfolio> GetModels();
    void SaveModel(ModelPortfolio model);

    // Payments (scoped per shopper)
    void AddPayment(PaymentRecord payment);
    IReadOnlyList<PaymentRecord> GetPayments(string shopperId);
    PaymentRecord? GetPayment(string shopperId, string paymentId);

    // Realignments (global)
    void AddRealignment(RealignmentRecord realignment);
    RealignmentRecord? GetRealignment(string realignmentId);
}
