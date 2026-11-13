using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.Upvest;

/// <summary>
/// The application's single boundary to Upvest. Every method wraps one Upvest Investment API operation, takes and
/// returns plain domain-friendly types (no SDK types leak out), and raises <see cref="UpvestProviderException"/> for
/// every failure. Implementations never log personal data or secrets.
/// </summary>
public interface IUpvestGateway
{
    // Flow 1 — investor onboarding
    Task<UpvestUserResult> CreateUserAsync(InvestorSignupDetails details, Guid idempotencyKey, CancellationToken ct);
    Task<Guid> CreateAccountGroupAsync(Guid userId, Guid idempotencyKey, CancellationToken ct);
    Task<UpvestAccountResult> CreateAccountAsync(Guid userId, Guid accountGroupId, Guid idempotencyKey, CancellationToken ct);
    Task<UpvestUserResult> GetUserAsync(Guid userId, CancellationToken ct);
    Task<string?> GetPortfolioAccountStatusAsync(Guid userId, Guid accountId, CancellationToken ct);

    // Flow 2 — model portfolios
    Task<UpvestAllocationResult> CreateAllocationAsync(string name, IReadOnlyList<FundWeight> funds, Guid idempotencyKey, CancellationToken ct);
    Task UpdateAllocationAsync(Guid allocationId, string name, IReadOnlyList<FundWeight> funds, CancellationToken ct);
    Task CreateConfigurationAsync(Guid accountId, Guid allocationId, Guid idempotencyKey, CancellationToken ct);
    Task UpdateConfigurationAsync(Guid accountId, Guid allocationId, Guid idempotencyKey, CancellationToken ct);
    Task<Guid?> GetFollowedAllocationAsync(Guid accountId, CancellationToken ct);
    Task<ISet<string>> ListTradableIsinsAsync(CancellationToken ct);

    // Flow 3 — paying in
    Task<UpvestTopupResult> CreateTopupAsync(Guid accountGroupId, decimal amount, Guid idempotencyKey, CancellationToken ct);
    Task<UpvestOrderResult> CreateBuyOrderAsync(Guid userId, Guid accountId, decimal amount, Guid idempotencyKey, CancellationToken ct);
    Task<string?> GetOrderStatusAsync(Guid orderId, CancellationToken ct);

    // Flow 4 — realignment
    Task<Guid> TriggerRealignmentAsync(Guid allocationId, Guid idempotencyKey, CancellationToken ct);
    Task<string> GetRealignmentStatusAsync(Guid executionId, CancellationToken ct);

    // Flow 5 — portfolio view
    Task<UpvestValuationResult> GetValuationAsync(Guid accountId, CancellationToken ct);
}
