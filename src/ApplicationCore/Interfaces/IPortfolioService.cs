using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Orchestrates the managed-investment-portfolio flows on behalf of a signed-in shopper or the
/// operator, wiring the shop's local records to Upvest. Every status returned reflects what has
/// actually happened at Upvest, learned through the shop's own calls.
/// </summary>
public interface IPortfolioService
{
    // Flow 1.
    Task<InvestorView> RegisterInvestorAsync(string buyerId, InvestorRegistration registration, CancellationToken ct);
    Task<InvestorView?> GetInvestorAsync(string buyerId, CancellationToken ct);

    // Flow 2.
    Task<ModelView> UpsertModelAsync(string modelId, string name, IReadOnlyList<FundWeight> funds, CancellationToken ct);
    Task<IReadOnlyList<ModelView>> ListModelsAsync(CancellationToken ct);
    Task SelectModelAsync(string buyerId, string modelId, CancellationToken ct);

    // Flow 3.
    Task<PaymentView> PayInAsync(string buyerId, decimal amount, CancellationToken ct);
    Task<IReadOnlyList<PaymentView>> ListPaymentsAsync(string buyerId, CancellationToken ct);

    // Flow 4.
    Task<RealignmentView> TriggerRealignmentAsync(string modelId, CancellationToken ct);
    Task<RealignmentView> GetRealignmentAsync(Guid realignmentId, CancellationToken ct);

    // Flow 5.
    Task<PortfolioView> GetPortfolioAsync(string buyerId, CancellationToken ct);
}

public record InvestorView(string InvestorId, string Status);

public record ModelFundView(string InstrumentId, decimal Weight);

public record ModelView(string ModelId, string Name, IReadOnlyList<ModelFundView> Funds);

public record PaymentView(Guid PaymentId, decimal Amount, string Status);

public record RealignmentView(Guid RealignmentId, string Status);

public record HoldingView(string InstrumentId, decimal Quantity, decimal Value);

public record PortfolioView(
    string? ModelId,
    IReadOnlyList<HoldingView> Holdings,
    decimal TotalValue,
    decimal PaidIn);
