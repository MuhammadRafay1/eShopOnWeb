using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;

/// <summary>
/// Typed gateway to the Upvest Investment API. Every method performs the HTTP-message-signed,
/// OAuth2-scoped call(s) the corresponding Upvest operation requires and maps the result onto the
/// shop's domain types. Implementations throw <see cref="UpvestApiException"/> on failure.
/// </summary>
public interface IUpvestClient
{
    // Flow 1 — becoming an investor.
    Task<UpvestUser> CreateUserAsync(InvestorRegistration registration, CancellationToken ct);
    Task<UpvestUser> GetUserAsync(string upvestUserId, CancellationToken ct);
    Task SetTaxResidencyAsync(string upvestUserId, string taxCountry, string taxId, CancellationToken ct);
    Task SubmitIdentityCheckAsync(string upvestUserId, InvestorRegistration registration, CancellationToken ct);
    Task<string> CreateAccountGroupAsync(string upvestUserId, CancellationToken ct);
    Task<UpvestAccount> CreateAccountAsync(string upvestUserId, string accountGroupId, CancellationToken ct);
    Task<UpvestAccount> GetAccountAsync(string accountId, CancellationToken ct);

    // Flow 2 — model portfolios (allocations) and the investor's choice (configuration).
    Task<string> CreateAllocationAsync(string name, IReadOnlyList<FundWeight> funds, CancellationToken ct);
    Task ReplaceAllocationAsync(string allocationId, string name, IReadOnlyList<FundWeight> funds, CancellationToken ct);
    Task SetAccountAllocationAsync(string accountId, string allocationId, CancellationToken ct);

    // Flow 3 — paying in.
    Task<string> CreateTopupAsync(string accountGroupId, decimal amount, string currency, CancellationToken ct);
    Task<UpvestOrder> CreatePortfolioBuyOrderAsync(string upvestUserId, string accountId, decimal amount, string currency, CancellationToken ct);
    Task<UpvestOrder> GetPortfolioOrderAsync(string orderId, CancellationToken ct);

    // Flow 4 — realignment (rebalancing).
    Task<string> TriggerRealignmentAsync(string allocationId, CancellationToken ct);
    Task<UpvestRebalancing> GetRealignmentAsync(string executionId, CancellationToken ct);

    // Flow 5 — the investor's portfolio.
    Task<UpvestValuation> GetValuationAsync(string accountId, CancellationToken ct);
}
