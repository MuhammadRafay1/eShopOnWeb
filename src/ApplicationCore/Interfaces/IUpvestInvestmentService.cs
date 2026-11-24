using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Upvest;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Abstraction over everything this application needs from Upvest. Every call is made directly to
/// Upvest (this application exposes no route for Upvest to call back); statuses are therefore
/// learned by polling. Implementations translate Upvest HTTP errors into
/// <see cref="Microsoft.eShopWeb.ApplicationCore.Upvest.UpvestApiException"/>.
/// </summary>
public interface IUpvestInvestmentService
{
    Task<UpvestUser> CreateUserAsync(UpvestUserRegistration registration, CancellationToken cancellationToken = default);
    Task<UpvestUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default);

    Task SetTaxResidenciesAsync(string userId, IReadOnlyList<UpvestTaxResidency> residencies, CancellationToken cancellationToken = default);
    Task<string> CreateKycCheckAsync(string userId, UpvestKycCheck check, CancellationToken cancellationToken = default);

    Task<UpvestAccountGroup> CreateAccountGroupAsync(string userId, CancellationToken cancellationToken = default);
    Task<UpvestAccountGroup?> GetAccountGroupAsync(string accountGroupId, CancellationToken cancellationToken = default);

    Task<UpvestAccount> CreateAccountAsync(string userId, string accountGroupId, CancellationToken cancellationToken = default);
    Task<UpvestAccount?> GetAccountAsync(string accountId, CancellationToken cancellationToken = default);

    Task<string> CreateAllocationAsync(string name, IReadOnlyList<UpvestAllocationEntry> entries, CancellationToken cancellationToken = default);
    Task CreateConfigurationAsync(string accountId, string allocationId, CancellationToken cancellationToken = default);

    Task<string> CreateBankTransactionAsync(UpvestBankTransaction transaction, CancellationToken cancellationToken = default);

    Task<UpvestOrder> PlaceOrderAsync(string userId, string accountId, decimal cashAmount, string currency, CancellationToken cancellationToken = default);
    Task<UpvestOrder?> GetOrderAsync(string orderId, CancellationToken cancellationToken = default);

    Task<string> TriggerRebalancingByAllocationAsync(string allocationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UpvestRebalancingOrder>> GetRebalancingOrdersAsync(string executionId, CancellationToken cancellationToken = default);

    Task<UpvestValuation?> GetAccountValuationAsync(string accountId, CancellationToken cancellationToken = default);
}
