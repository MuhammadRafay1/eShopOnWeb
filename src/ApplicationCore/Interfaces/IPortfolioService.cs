using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Result;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Orchestrates the managed-investment-portfolio flows on top of <see cref="IUpvestGateway"/> and
/// the shop's own persistence. Every status returned reflects what actually happened at Upvest,
/// read back through the gateway on demand.
/// </summary>
public interface IPortfolioService
{
    Task<Result<InvestorView>> RegisterInvestorAsync(string buyerId, InvestorSignUp signUp, CancellationToken ct = default);
    Task<Result<InvestorView>> GetInvestorAsync(string buyerId, CancellationToken ct = default);

    Task<Result<ModelView>> UpsertModelAsync(string modelId, string name, IEnumerable<FundView> funds, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ModelView>>> GetModelsAsync(CancellationToken ct = default);
    Task<Result> SelectModelAsync(string buyerId, string modelId, CancellationToken ct = default);

    Task<Result<PaymentView>> PayAsync(string buyerId, decimal amount, CancellationToken ct = default);
    Task<Result<IReadOnlyList<PaymentView>>> GetPaymentsAsync(string buyerId, CancellationToken ct = default);

    Task<Result<RealignmentView>> TriggerRealignmentAsync(string modelId, CancellationToken ct = default);
    Task<Result<RealignmentView>> GetRealignmentAsync(Guid realignmentId, CancellationToken ct = default);

    Task<Result<PortfolioView>> GetPortfolioAsync(string buyerId, CancellationToken ct = default);
}

public record InvestorView(Guid InvestorId, string Status);
public record FundView(string InstrumentId, decimal Weight);
public record ModelView(string ModelId, string Name, IReadOnlyList<FundView> Funds);
public record PaymentView(Guid PaymentId, decimal Amount, string Status);
public record RealignmentView(Guid RealignmentId, string Status);
public record HoldingView(string InstrumentId, decimal Quantity, decimal Value);
public record PortfolioView(string? ModelId, IReadOnlyList<HoldingView> Holdings, decimal TotalValue, decimal PaidIn);
