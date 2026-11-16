using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

// Caller-facing view models returned by the application services. The PublicApi endpoints map these to
// their HTTP response DTOs (whose field names are fixed by the task).

public sealed record InvestorView(Guid InvestorId, InvestorStatus Status);

public sealed record ModelView(string ModelId, string Name, IReadOnlyList<FundWeight> Funds);

public sealed record PaymentView(Guid PaymentId, decimal Amount, PaymentStatus Status);

public sealed record RealignmentView(Guid RealignmentId, RealignmentStatus Status);

public sealed record PortfolioView(
    string? ModelId,
    IReadOnlyList<PortfolioHolding> Holdings,
    decimal TotalValue,
    decimal PaidIn);

/// <summary>Flow 1 — a shopper becoming an investor.</summary>
public interface IPortfolioInvestorService
{
    Task<InvestorView> SignUpAsync(string shopperUserName, InvestorSignup signup, CancellationToken ct);

    /// <summary>The caller's sign-up status, reconciled against Upvest; null if the caller is not an investor.</summary>
    Task<InvestorView?> GetStatusAsync(string shopperUserName, CancellationToken ct);
}

/// <summary>Flow 2 — operator model portfolios and the investor's model choice.</summary>
public interface IModelPortfolioService
{
    /// <summary>Operator action: create or replace a model portfolio.</summary>
    Task<ModelView> UpsertModelAsync(string modelKey, string name, IReadOnlyList<FundWeight> funds, CancellationToken ct);

    Task<IReadOnlyList<ModelView>> ListModelsAsync(CancellationToken ct);

    /// <summary>The signed-in investor chooses which model their portfolio follows.</summary>
    Task ChooseModelAsync(string shopperUserName, string modelKey, CancellationToken ct);
}

/// <summary>Flow 3 — paying in.</summary>
public interface IPortfolioPaymentService
{
    Task<PaymentView> PayInAsync(string shopperUserName, decimal amountEuros, CancellationToken ct);

    /// <summary>The caller's payments, newest first, reconciled against Upvest.</summary>
    Task<IReadOnlyList<PaymentView>> ListPaymentsAsync(string shopperUserName, CancellationToken ct);
}

/// <summary>Flow 4 — realignment.</summary>
public interface IPortfolioRealignmentService
{
    /// <summary>Operator action: realign every portfolio following a model.</summary>
    Task<RealignmentView> TriggerAsync(string modelKey, CancellationToken ct);

    /// <summary>A realignment's status, reconciled against Upvest; null if unknown to this app.</summary>
    Task<RealignmentView?> GetAsync(Guid realignmentKey, CancellationToken ct);
}

/// <summary>Flow 5 — the investor's portfolio.</summary>
public interface IPortfolioQueryService
{
    /// <summary>The caller's portfolio as Upvest records it; null if the caller is not an investor.</summary>
    Task<PortfolioView?> GetPortfolioAsync(string shopperUserName, CancellationToken ct);
}
