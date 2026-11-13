using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolio;

/// <summary>Flow 1 — becoming an investor.</summary>
public interface IInvestorService
{
    /// <summary>Sign the shopper up as an investor with Upvest. Idempotent per shopper.</summary>
    Task<InvestorStatusResult> SignUpAsync(string shopperId, InvestorSignupDetails details, CancellationToken ct);

    /// <summary>The caller's current sign-up status, as it currently stands at Upvest. Null if never signed up.</summary>
    Task<InvestorStatusResult?> GetStatusAsync(string shopperId, CancellationToken ct);
}

/// <summary>Flow 2 — model portfolios (operator) and choosing a model (investor).</summary>
public interface IModelPortfolioService
{
    /// <summary>Operator: create or replace a model portfolio. Weights must sum to exactly 100.</summary>
    Task<ModelResult> UpsertModelAsync(string modelId, string name, IReadOnlyList<FundWeight> funds, CancellationToken ct);

    /// <summary>Every model portfolio and its funds.</summary>
    Task<IReadOnlyList<ModelResult>> ListModelsAsync(CancellationToken ct);

    /// <summary>Investor: choose which model their portfolio follows (or switch to another).</summary>
    Task ChooseModelAsync(string shopperId, string modelId, CancellationToken ct);
}

/// <summary>Flow 3 — paying in.</summary>
public interface IPaymentService
{
    /// <summary>Pay an amount (euros) in, invested across the caller's model's funds in its weights.</summary>
    Task<PaymentResult> PayInAsync(string shopperId, decimal amount, CancellationToken ct);

    /// <summary>The caller's payments, newest first, each with its current status at Upvest.</summary>
    Task<IReadOnlyList<PaymentResult>> ListPaymentsAsync(string shopperId, CancellationToken ct);
}

/// <summary>Flow 4 — realignment (operator).</summary>
public interface IRealignmentService
{
    /// <summary>Realign every portfolio following the model back to the model's current weights.</summary>
    Task<RealignmentResult> TriggerAsync(string modelId, CancellationToken ct);

    /// <summary>The current status of a realignment at Upvest. Null if unknown.</summary>
    Task<RealignmentResult?> GetAsync(Guid realignmentId, CancellationToken ct);
}

/// <summary>Flow 5 — the investor's portfolio as Upvest currently records it.</summary>
public interface IPortfolioService
{
    /// <summary>The caller's portfolio: model, holdings, total value, and total paid in. Null if not an investor.</summary>
    Task<PortfolioView?> GetPortfolioAsync(string shopperId, CancellationToken ct);
}
