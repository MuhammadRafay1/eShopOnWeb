using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services.Portfolio;

public class InvestorService : IInvestorService
{
    private readonly IPortfolioRepository<Investor> _investors;
    private readonly IPortfolioRepository<ModelPortfolio> _models;
    private readonly IUpvestInvestmentGateway _gateway;
    private readonly IAppLogger<InvestorService> _logger;

    public InvestorService(
        IPortfolioRepository<Investor> investors,
        IPortfolioRepository<ModelPortfolio> models,
        IUpvestInvestmentGateway gateway,
        IAppLogger<InvestorService> logger)
    {
        _investors = investors;
        _models = models;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<InvestorSummary> SignUpAsync(string shopperId, InvestorRegistration registration, CancellationToken cancellationToken)
    {
        // One investor per shopper: if the shopper already signed up, return where it stands rather
        // than onboarding a second time at Upvest.
        var existing = await _investors.GetByIdAsync(shopperId, cancellationToken);
        if (existing is not null)
        {
            return new InvestorSummary(existing.InvestorId, existing.Status);
        }

        var investor = new Investor(shopperId);
        try
        {
            // Claim first: the store's primary key refuses a concurrent second sign-up for the same
            // shopper before any Upvest call is made.
            await _investors.AddAsync(investor, cancellationToken);
        }
        catch (Exception) // duplicate key from a racing sign-up
        {
            var raced = await _investors.GetByIdAsync(shopperId, cancellationToken);
            if (raced is not null)
            {
                return new InvestorSummary(raced.InvestorId, raced.Status);
            }
            throw;
        }

        await RunOnboardingAsync(investor, registration, cancellationToken);
        await _investors.UpdateAsync(investor, cancellationToken);
        return new InvestorSummary(investor.InvestorId, investor.Status);
    }

    public async Task<InvestorSummary?> GetAsync(string shopperId, CancellationToken cancellationToken)
    {
        var investor = await _investors.GetByIdAsync(shopperId, cancellationToken);
        if (investor is null)
        {
            return null;
        }

        // Status must reflect what actually happened at Upvest — re-read on every GET. Best-effort:
        // where the account steps or status reads are not yet available (e.g. scopes not granted),
        // the record keeps its current state rather than failing the read.
        try
        {
            await AdvanceAccountAsync(investor, cancellationToken);
            await RefreshStatusAsync(investor, cancellationToken);
            await _investors.UpdateAsync(investor, cancellationToken);
        }
        catch (UpvestGatewayException ex)
        {
            _logger.LogWarning("Could not refresh investor status from Upvest (status={Status}, scopeWall={ScopeWall}).", ex.StatusCode, ex.IsScopeWall);
        }

        return new InvestorSummary(investor.InvestorId, investor.Status);
    }

    public async Task ChooseModelAsync(string shopperId, string modelId, CancellationToken cancellationToken)
    {
        var investor = await _investors.GetByIdAsync(shopperId, cancellationToken);
        if (investor is null)
        {
            throw new InvestorNotFoundException();
        }

        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByIdSpec(modelId), cancellationToken);
        if (model is null)
        {
            throw new ModelPortfolioNotFoundException(modelId);
        }

        investor.ChooseModel(modelId);
        await _investors.UpdateAsync(investor, cancellationToken);

        // Make the choice take effect at Upvest where possible: link the investor's account to the
        // model's allocation via a portfolio configuration. If the account/allocation are not yet
        // provisioned (scope wall), the app-side choice still stands and takes effect once they are.
        if (investor.UpvestAccountId is Guid accountId && model.UpvestAllocationId is Guid allocationId)
        {
            try
            {
                var current = await _gateway.GetConfiguredAllocationAsync(accountId, cancellationToken);
                if (current is null)
                {
                    await _gateway.CreateConfigurationAsync(accountId, allocationId, investor.ConfigurationIdempotencyKey, cancellationToken);
                }
                else if (current != allocationId)
                {
                    await _gateway.UpdateConfigurationAsync(accountId, allocationId, investor.ConfigurationIdempotencyKey, cancellationToken);
                }
            }
            catch (UpvestGatewayException ex) when (ex.IsScopeWall || ex.OutcomeUnknown)
            {
                _logger.LogWarning("Portfolio configuration deferred (scopeWall={ScopeWall}, unknown={Unknown}).", ex.IsScopeWall, ex.OutcomeUnknown);
            }
        }
    }

    // --- onboarding orchestration -------------------------------------------------------------

    private async Task RunOnboardingAsync(Investor investor, InvestorRegistration registration, CancellationToken cancellationToken)
    {
        try
        {
            var userId = await _gateway.CreateUserAsync(registration, investor.UserIdempotencyKey, cancellationToken);
            investor.SetUpvestUserId(userId);
            await _investors.UpdateAsync(investor, cancellationToken);

            await _gateway.CreateIdentifierAsync(userId, registration.IdentityDocument, cancellationToken);
            await _gateway.SetTaxResidenciesAsync(userId, registration.TaxId, registration.TaxCountry, investor.TaxIdempotencyKey, cancellationToken);
            await _gateway.CreateUserCheckAsync(userId, cancellationToken);

            await AdvanceAccountAsync(investor, cancellationToken);
            await RefreshStatusAsync(investor, cancellationToken);
        }
        catch (UpvestGatewayException ex) when (ex.IsScopeWall)
        {
            investor.SetNote("Awaiting provider authorisation for onboarding scopes; sign-up will complete once granted.");
            _logger.LogWarning("Onboarding paused at scope wall for a sign-up (status pending).");
        }
        catch (UpvestGatewayException ex) when (ex.OutcomeUnknown)
        {
            investor.SetNote("Sign-up submitted; awaiting confirmation from the provider.");
            _logger.LogWarning("Onboarding outcome unknown for a sign-up; will reconcile on next read.");
        }
        catch (UpvestGatewayException ex) when (IsClientError(ex.StatusCode))
        {
            investor.SetStatus(InvestorStatus.Rejected);
            investor.SetNote("The provider declined the sign-up.");
            _logger.LogWarning("Provider declined a sign-up (status={Status}).", ex.StatusCode);
        }
        catch (UpvestGatewayException ex)
        {
            // Transient/server-side — keep pending and let a later read reconcile.
            investor.SetNote("Sign-up in progress; the provider is temporarily unavailable.");
            _logger.LogWarning("Transient provider error during onboarding (status={Status}).", ex.StatusCode);
        }
    }

    private async Task AdvanceAccountAsync(Investor investor, CancellationToken cancellationToken)
    {
        if (investor.UpvestUserId is not Guid userId)
        {
            return;
        }

        if (investor.UpvestAccountGroupId is null)
        {
            var groupId = await _gateway.CreateAccountGroupAsync(userId, investor.AccountGroupIdempotencyKey, cancellationToken);
            investor.SetAccountGroupId(groupId);
            await _investors.UpdateAsync(investor, cancellationToken);
        }

        if (investor.UpvestAccountId is null && investor.UpvestAccountGroupId is Guid accountGroupId)
        {
            var accountId = await _gateway.CreateAccountAsync(userId, accountGroupId, investor.AccountIdempotencyKey, cancellationToken);
            investor.SetAccountId(accountId);
            await _investors.UpdateAsync(investor, cancellationToken);
        }
    }

    private async Task RefreshStatusAsync(Investor investor, CancellationToken cancellationToken)
    {
        if (investor.Status == InvestorStatus.Rejected || investor.UpvestUserId is not Guid userId)
        {
            return;
        }

        var userStatus = await _gateway.GetUserStatusAsync(userId, cancellationToken);
        var accountStatus = investor.UpvestAccountId is Guid accountId
            ? await _gateway.GetAccountStatusAsync(accountId, cancellationToken)
            : UpvestAccountStatus.Unknown;

        if (accountStatus == UpvestAccountStatus.Closed)
        {
            investor.SetStatus(InvestorStatus.Rejected);
        }
        else if (userStatus == UpvestUserStatus.Active && accountStatus == UpvestAccountStatus.Active)
        {
            investor.SetStatus(InvestorStatus.Active);
        }
        else
        {
            investor.SetStatus(InvestorStatus.Pending);
        }
    }

    private static bool IsClientError(HttpStatusCode? status) =>
        status is >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError
        && status != HttpStatusCode.TooManyRequests
        && status != HttpStatusCode.RequestTimeout;
}
