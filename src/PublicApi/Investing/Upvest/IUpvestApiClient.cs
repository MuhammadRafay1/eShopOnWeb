using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.PublicApi.Investing.Upvest;

/// <summary>
/// A thin, typed client over the Upvest Investment API. Every method goes through the single
/// authenticating <see cref="UpvestAuthenticationHandler"/>; no method attaches credentials itself.
/// </summary>
public interface IUpvestApiClient
{
    Task<UpvestUser> CreateUserAsync(EnrolmentDetails details, CancellationToken cancellationToken = default);
    Task SubmitKycCheckAsync(string userId, EnrolmentDetails details, CancellationToken cancellationToken = default);
    Task SubmitInstrumentFitCheckAsync(string userId, CancellationToken cancellationToken = default);
    Task SetTaxResidenciesAsync(string userId, EnrolmentDetails details, CancellationToken cancellationToken = default);
    Task CreateNationalIdentifierAsync(string userId, EnrolmentDetails details, CancellationToken cancellationToken = default);
    Task<string> CreateAccountGroupAsync(string userId, CancellationToken cancellationToken = default);
    Task<string> CreateAccountAsync(string userId, string accountGroupId, CancellationToken cancellationToken = default);

    /// <summary>Returns the id of the user's existing account group, or null if they have none.</summary>
    Task<string?> FindAccountGroupIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Returns the id of the user's existing TRADING account, or null if they have none.</summary>
    Task<string?> FindTradingAccountIdAsync(string userId, CancellationToken cancellationToken = default);

    Task<string> GetUserStatusAsync(string userId, CancellationToken cancellationToken = default);
    Task<string> GetAccountStatusAsync(string accountId, CancellationToken cancellationToken = default);

    Task FundAccountGroupAsync(string accountGroupId, decimal amount, CancellationToken cancellationToken = default);
    Task<string> PlaceBuyOrderAsync(string userId, string accountId, decimal cashAmount, CancellationToken cancellationToken = default);
    Task<UpvestOrderSnapshot> GetOrderAsync(string orderId, CancellationToken cancellationToken = default);

    Task EnsureWebhookAsync(string callbackUrl, CancellationToken cancellationToken = default);
}
