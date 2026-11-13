using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;

namespace PublicApiIntegrationTests.Portfolio;

/// <summary>
/// A deterministic in-memory stand-in for the Upvest boundary, so endpoint tests exercise the full HTTP
/// stack (auth, routing, persistence, status mapping) without reaching the sandbox.
/// </summary>
public class FakeUpvestPortfolioService : IUpvestPortfolioService
{
    public InvestorStatus InvestorStatus { get; set; } = InvestorStatus.Active;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Invested;
    public RealignmentStatus RealignmentStatus { get; set; } = RealignmentStatus.Completed;

    public Task<Guid> CreateUserAsync(InvestorOnboardingData data, Guid idempotencyKey, CancellationToken ct)
        => Task.FromResult(Guid.NewGuid());

    public Task SetTaxResidenciesAsync(Guid userId, string taxCountry, string taxId, Guid idempotencyKey, CancellationToken ct)
        => Task.CompletedTask;

    public Task<Guid> CreateAccountGroupAsync(Guid userId, Guid idempotencyKey, CancellationToken ct)
        => Task.FromResult(Guid.NewGuid());

    public Task<Guid> CreateAccountAsync(Guid userId, Guid accountGroupId, string? name, Guid idempotencyKey, CancellationToken ct)
        => Task.FromResult(Guid.NewGuid());

    public Task<InvestorStatus> GetInvestorStatusAsync(Guid userId, Guid? accountId, CancellationToken ct)
        => Task.FromResult(InvestorStatus);

    public Task<InstrumentCheck> CheckInstrumentsAsync(IReadOnlyCollection<string> isins, CancellationToken ct)
        => Task.FromResult(new InstrumentCheck(true, Array.Empty<string>()));

    public Task<Guid> CreateModelAllocationAsync(string name, IReadOnlyList<FundWeight> funds, Guid idempotencyKey, CancellationToken ct)
        => Task.FromResult(Guid.NewGuid());

    public Task UpdateModelAllocationAsync(Guid allocationId, string name, IReadOnlyList<FundWeight> funds, CancellationToken ct)
        => Task.CompletedTask;

    public Task CreateInvestorConfigurationAsync(Guid accountId, Guid allocationId, Guid idempotencyKey, CancellationToken ct)
        => Task.CompletedTask;

    public Task UpdateInvestorConfigurationAsync(Guid accountId, Guid allocationId, Guid idempotencyKey, CancellationToken ct)
        => Task.CompletedTask;

    public Task<Guid> CreateTopUpAsync(Guid accountGroupId, decimal amount, Guid idempotencyKey, CancellationToken ct)
        => Task.FromResult(Guid.NewGuid());

    public Task<Guid> PlacePortfolioOrderAsync(Guid userId, Guid accountId, decimal amount, Guid idempotencyKey, CancellationToken ct)
        => Task.FromResult(Guid.NewGuid());

    public Task<PaymentStatus> GetPaymentStatusAsync(Guid? topUpId, Guid? orderId, CancellationToken ct)
        => Task.FromResult(PaymentStatus);

    public Task<Guid> TriggerRealignmentAsync(Guid allocationId, Guid idempotencyKey, CancellationToken ct)
        => Task.FromResult(Guid.NewGuid());

    public Task<RealignmentStatus> GetRealignmentStatusAsync(Guid executionId, CancellationToken ct)
        => Task.FromResult(RealignmentStatus);

    public Task<PortfolioView> GetPortfolioAsync(Guid accountId, CancellationToken ct)
        => Task.FromResult(new PortfolioView(
            Guid.NewGuid(),
            new List<PortfolioHolding> { new("DE0000000001", 1.5m, 150.00m) },
            150.00m));
}
