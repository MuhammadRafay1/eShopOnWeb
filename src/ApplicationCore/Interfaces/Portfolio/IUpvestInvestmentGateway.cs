using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;

/// <summary>
/// The application's boundary over the Upvest Investment API. Every method is domain-shaped — no
/// SDK type crosses it — and translates SDK failures into <see cref="UpvestGatewayException"/>.
/// Writes that take a real Upvest idempotency key accept one so a retry replays rather than
/// duplicates. Statuses are learned only through these reads: the application exposes no webhook.
/// </summary>
public interface IUpvestInvestmentGateway
{
    // --- Flow 1: becoming an investor ---------------------------------------------------------

    Task<Guid> CreateUserAsync(InvestorRegistration registration, Guid idempotencyKey, CancellationToken cancellationToken);
    Task CreateIdentifierAsync(Guid userId, RegistrationIdentityDocument document, CancellationToken cancellationToken);
    Task SetTaxResidenciesAsync(Guid userId, string taxId, string taxCountry, Guid idempotencyKey, CancellationToken cancellationToken);
    Task CreateUserCheckAsync(Guid userId, CancellationToken cancellationToken);
    Task<Guid> CreateAccountGroupAsync(Guid userId, Guid idempotencyKey, CancellationToken cancellationToken);
    Task<Guid> CreateAccountAsync(Guid userId, Guid accountGroupId, Guid idempotencyKey, CancellationToken cancellationToken);

    Task<UpvestUserStatus> GetUserStatusAsync(Guid userId, CancellationToken cancellationToken);
    Task<UpvestAccountStatus> GetAccountStatusAsync(Guid accountId, CancellationToken cancellationToken);

    // --- Flow 2: model portfolios -------------------------------------------------------------

    Task<Guid> CreateAllocationAsync(string name, IReadOnlyList<FundWeight> funds, Guid idempotencyKey, CancellationToken cancellationToken);
    Task UpdateAllocationAsync(Guid allocationId, string name, IReadOnlyList<FundWeight> funds, CancellationToken cancellationToken);
    Task<IReadOnlyList<AllocationSummary>> ListAllocationsAsync(CancellationToken cancellationToken);
    Task<bool> InstrumentExistsAsync(string isin, CancellationToken cancellationToken);

    Task CreateConfigurationAsync(Guid accountId, Guid allocationId, Guid idempotencyKey, CancellationToken cancellationToken);
    Task UpdateConfigurationAsync(Guid accountId, Guid allocationId, Guid idempotencyKey, CancellationToken cancellationToken);
    Task<Guid?> GetConfiguredAllocationAsync(Guid accountId, CancellationToken cancellationToken);

    // --- Flow 3: paying in --------------------------------------------------------------------

    Task<Guid> CreateTopupAsync(Guid accountGroupId, decimal amount, Guid idempotencyKey, CancellationToken cancellationToken);
    Task<UpvestLegStatus> GetTopupStatusAsync(Guid topupId, CancellationToken cancellationToken);
    Task<Guid> CreateOrderAsync(Guid userId, Guid accountId, decimal amount, Guid idempotencyKey, CancellationToken cancellationToken);
    Task<UpvestLegStatus> GetOrderStatusAsync(Guid orderId, CancellationToken cancellationToken);

    // --- Flow 4: realignment ------------------------------------------------------------------

    Task<Guid> TriggerRebalancingAsync(Guid allocationId, Guid idempotencyKey, CancellationToken cancellationToken);
    Task<UpvestRealignmentStatus> GetRebalancingStatusAsync(Guid executionId, CancellationToken cancellationToken);

    // --- Flow 5: the investor's portfolio -----------------------------------------------------

    Task<IReadOnlyList<Position>> GetPositionsAsync(Guid accountId, CancellationToken cancellationToken);
    Task<PortfolioValuation> GetValuationAsync(Guid accountId, CancellationToken cancellationToken);
}
