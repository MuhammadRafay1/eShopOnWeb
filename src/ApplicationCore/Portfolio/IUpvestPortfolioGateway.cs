using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolio;

/// <summary>
/// The sole boundary between this application and Upvest. Each method wraps one Upvest operation (or
/// a tightly-coupled read), translating transport and API failures into <see cref="UpvestGatewayException"/>.
/// All contract facts come from the Upvest .NET SDK; nothing here leaks SDK types to the domain.
/// </summary>
public interface IUpvestPortfolioGateway
{
    // --- Flow 1: becoming an investor (provisioning, step by step so progress can be persisted) ---

    Task<Guid> CreateUserAsync(InvestorRegistration registration, Guid idempotencyKey, CancellationToken ct);

    Task<Guid> CreateAccountGroupAsync(Guid upvestUserId, Guid idempotencyKey, CancellationToken ct);

    Task<AccountProvisioned> CreateAccountAsync(Guid upvestUserId, Guid accountGroupId, Guid idempotencyKey, CancellationToken ct);

    Task<Guid> CreateKycCheckAsync(Guid upvestUserId, IdentityDocument document, string nationality, CancellationToken ct);

    /// <summary>Re-reads KYC and account state at Upvest to settle the sign-up status.</summary>
    Task<InvestorAcceptance> GetAcceptanceAsync(Guid upvestUserId, Guid accountId, CancellationToken ct);

    // --- Flow 2: model portfolios ---

    /// <summary>Confirms every ISIN is an instrument Upvest offers; throws if any is not.</summary>
    Task EnsureInstrumentsExistAsync(IReadOnlyCollection<string> isins, CancellationToken ct);

    Task<Guid> CreateModelAllocationAsync(string name, IReadOnlyList<ModelFund> funds, Guid idempotencyKey, CancellationToken ct);

    Task UpdateModelAllocationAsync(Guid allocationId, string name, IReadOnlyList<ModelFund> funds, CancellationToken ct);

    Task CreateInvestorConfigurationAsync(Guid accountId, Guid allocationId, Guid idempotencyKey, CancellationToken ct);

    Task UpdateInvestorConfigurationAsync(Guid accountId, Guid allocationId, Guid idempotencyKey, CancellationToken ct);

    // --- Flow 3: paying in ---

    Task CreditCashAsync(Guid accountGroupId, decimal amount, Guid idempotencyKey, CancellationToken ct);

    Task<Guid> PlaceOrderAsync(Guid upvestUserId, Guid accountId, decimal amount, Guid idempotencyKey, CancellationToken ct);

    Task<ProviderOutcome> GetOrderOutcomeAsync(Guid orderId, CancellationToken ct);

    // --- Flow 4: realignment ---

    Task<Guid> TriggerRealignmentAsync(Guid allocationId, Guid idempotencyKey, CancellationToken ct);

    Task<ProviderOutcome> GetRealignmentOutcomeAsync(Guid executionId, CancellationToken ct);

    // --- Flow 5: the investor's portfolio ---

    Task<PortfolioSnapshot> GetPortfolioAsync(Guid accountId, CancellationToken ct);
}
