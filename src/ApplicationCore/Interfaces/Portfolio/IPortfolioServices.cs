using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;

// --- Result DTOs returned to the API layer (no SDK or EF types leak) ---------------------------

public record InvestorSummary(Guid InvestorId, InvestorStatus Status);

public record ModelSummary(string ModelId, string Name, IReadOnlyList<FundWeight> Funds);

public record PaymentSummary(Guid PaymentId, decimal Amount, PaymentStatus Status);

public record RealignmentSummary(Guid RealignmentId, RealignmentStatus Status);

public record PortfolioSummary(string? ModelId, IReadOnlyList<Holding> Holdings, decimal TotalValue, decimal PaidIn);

/// <summary>Flow 1 — signing a shopper up as an investor and reporting where it has got to.</summary>
public interface IInvestorService
{
    Task<InvestorSummary> SignUpAsync(string shopperId, InvestorRegistration registration, CancellationToken cancellationToken);
    Task<InvestorSummary?> GetAsync(string shopperId, CancellationToken cancellationToken);
    Task ChooseModelAsync(string shopperId, string modelId, CancellationToken cancellationToken);
}

/// <summary>Flow 2 — operator-defined model portfolios.</summary>
public interface IModelPortfolioService
{
    Task PutModelAsync(string modelId, string name, IReadOnlyList<FundWeight> funds, CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelSummary>> ListAsync(CancellationToken cancellationToken);
}

/// <summary>Flow 3 — paying in.</summary>
public interface IPaymentService
{
    Task<PaymentSummary> PayInAsync(string shopperId, decimal amount, string? idempotencyKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<PaymentSummary>> ListAsync(string shopperId, CancellationToken cancellationToken);
}

/// <summary>Flow 4 — realignment.</summary>
public interface IRealignmentService
{
    Task<RealignmentSummary> TriggerAsync(string modelId, CancellationToken cancellationToken);
    Task<RealignmentSummary?> GetAsync(Guid realignmentId, CancellationToken cancellationToken);
}

/// <summary>Flow 5 — the investor's portfolio.</summary>
public interface IPortfolioQueryService
{
    Task<PortfolioSummary?> GetAsync(string shopperId, CancellationToken cancellationToken);
}
