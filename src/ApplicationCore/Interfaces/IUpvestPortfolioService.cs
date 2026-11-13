using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The boundary to Upvest. Every method translates SDK/transport failures into
/// <see cref="Exceptions.UpvestIntegrationException"/>; no Upvest SDK type crosses this interface.
/// Status values are always derived from reads against Upvest, never assumed.
/// </summary>
public interface IUpvestPortfolioService
{
    // --- Flow 1: onboarding ---
    Task<Guid> CreateUserAsync(InvestorOnboardingData data, Guid idempotencyKey, CancellationToken cancellationToken);
    Task SetTaxResidenciesAsync(Guid userId, string taxCountry, string taxId, Guid idempotencyKey, CancellationToken cancellationToken);
    Task<Guid> CreateAccountGroupAsync(Guid userId, Guid idempotencyKey, CancellationToken cancellationToken);
    Task<Guid> CreateAccountAsync(Guid userId, Guid accountGroupId, string? name, Guid idempotencyKey, CancellationToken cancellationToken);
    Task<InvestorStatus> GetInvestorStatusAsync(Guid userId, Guid? accountId, CancellationToken cancellationToken);

    // --- Flow 2: model portfolios ---
    Task<InstrumentCheck> CheckInstrumentsAsync(IReadOnlyCollection<string> isins, CancellationToken cancellationToken);
    Task<Guid> CreateModelAllocationAsync(string name, IReadOnlyList<FundWeight> funds, Guid idempotencyKey, CancellationToken cancellationToken);
    Task UpdateModelAllocationAsync(Guid allocationId, string name, IReadOnlyList<FundWeight> funds, CancellationToken cancellationToken);
    Task CreateInvestorConfigurationAsync(Guid accountId, Guid allocationId, Guid idempotencyKey, CancellationToken cancellationToken);
    Task UpdateInvestorConfigurationAsync(Guid accountId, Guid allocationId, Guid idempotencyKey, CancellationToken cancellationToken);

    // --- Flow 3: paying in ---
    Task<Guid> CreateTopUpAsync(Guid accountGroupId, decimal amount, Guid idempotencyKey, CancellationToken cancellationToken);
    Task<Guid> PlacePortfolioOrderAsync(Guid userId, Guid accountId, decimal amount, Guid idempotencyKey, CancellationToken cancellationToken);
    Task<PaymentStatus> GetPaymentStatusAsync(Guid? topUpId, Guid? orderId, CancellationToken cancellationToken);

    // --- Flow 4: realignment ---
    Task<Guid> TriggerRealignmentAsync(Guid allocationId, Guid idempotencyKey, CancellationToken cancellationToken);
    Task<RealignmentStatus> GetRealignmentStatusAsync(Guid executionId, CancellationToken cancellationToken);

    // --- Flow 5: the portfolio ---
    Task<PortfolioView> GetPortfolioAsync(Guid accountId, CancellationToken cancellationToken);
}
