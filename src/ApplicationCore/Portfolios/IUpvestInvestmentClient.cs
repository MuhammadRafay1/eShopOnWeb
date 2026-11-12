using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolios;

/// <summary>
/// The whole surface of the Upvest Investment API this integration talks to. Every method signs its
/// request with HTTP message signatures and authenticates with a scoped OAuth2 token, per Upvest's spec.
/// Implementations must never log or echo personal data or secrets.
/// </summary>
public interface IUpvestInvestmentClient
{
    // --- Investor onboarding ---
    Task<UpvestUser> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UpvestUser> GetUserAsync(string userId, CancellationToken ct = default);

    /// <summary>Submits the KYC check derived from the shop's identity verification.</summary>
    Task<string> SubmitKycCheckAsync(string userId, UpvestIdentityDocument document, string nationality, CancellationToken ct = default);

    /// <summary>Declares the user's tax residency and TIN.</summary>
    Task SetTaxResidencyAsync(string userId, string taxCountry, string taxId, CancellationToken ct = default);

    /// <summary>Creates a regulatory identifier for the user (skipped by callers for CONCAT nationalities).</summary>
    Task CreatePassportIdentifierAsync(string userId, string issuingCountry, string number, CancellationToken ct = default);

    Task<UpvestAccountGroup> CreateAccountGroupAsync(string userId, CancellationToken ct = default);
    Task<UpvestAccount> CreatePortfolioAccountAsync(string userId, string accountGroupId, string name, CancellationToken ct = default);
    Task<UpvestAccount> GetAccountAsync(string accountId, CancellationToken ct = default);

    // --- Model portfolios (allocations) ---
    Task<UpvestAllocation> CreateAllocationAsync(string name, IReadOnlyList<UpvestAllocationFund> funds, CancellationToken ct = default);
    Task<UpvestAllocation> ReplaceAllocationAsync(string allocationId, string name, IReadOnlyList<UpvestAllocationFund> funds, CancellationToken ct = default);

    /// <summary>Links an account to an allocation, or re-links it to a different allocation.</summary>
    Task SetPortfolioConfigurationAsync(string accountId, string allocationId, CancellationToken ct = default);

    // --- Paying in ---
    Task<UpvestTopup> CreateTopupAsync(string accountGroupId, decimal amount, string currency, CancellationToken ct = default);
    Task<UpvestPortfolioOrder> CreateBuyOrderAsync(string userId, string accountId, decimal amount, string currency, CancellationToken ct = default);
    Task<UpvestPortfolioOrder> GetPortfolioOrderAsync(string portfolioOrderId, CancellationToken ct = default);

    // --- Realignment (rebalancing) ---
    Task<UpvestRebalancingExecution> CreateRebalancingForAllocationAsync(string allocationId, CancellationToken ct = default);

    /// <summary>Returns the statuses of the individual orders produced by a rebalancing execution.</summary>
    Task<IReadOnlyList<string>> GetRebalancingOrderStatusesAsync(string executionId, CancellationToken ct = default);

    // --- Portfolio valuation ---
    Task<UpvestValuation> GetCurrentValuationAsync(string accountId, CancellationToken ct = default);
}

/// <summary>
/// Raised when Upvest returns an error. Carries the HTTP status so callers can distinguish
/// "not authorised in this sandbox" (401) from genuine failures. Never carries request bodies.
/// </summary>
public class UpvestApiException : System.Exception
{
    public int StatusCode { get; }

    public UpvestApiException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}
