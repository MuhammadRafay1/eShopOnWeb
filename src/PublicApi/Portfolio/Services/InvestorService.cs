using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.Services;

public interface IInvestorService
{
    Task<InvestorResult> RegisterAsync(string shopperId, InvestorRegistration registration, CancellationToken ct);
    Task<InvestorResult?> GetAsync(string shopperId, CancellationToken ct);
}

/// <summary>
/// Orchestrates investor onboarding: it claims the app-side <see cref="Investor"/> row first (so a
/// concurrent duplicate sign-up cannot provision a second investor), then provisions the Upvest
/// resources, persisting each id as it is created so the flow is resumable and every status is read
/// back from Upvest.
/// </summary>
public sealed class InvestorService : IInvestorService
{
    private readonly IRepository<Investor> _investors;
    private readonly IUpvestInvestmentGateway _gateway;
    private readonly ILogger<InvestorService> _logger;

    public InvestorService(IRepository<Investor> investors, IUpvestInvestmentGateway gateway, ILogger<InvestorService> logger)
    {
        _investors = investors;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<InvestorResult> RegisterAsync(string shopperId, InvestorRegistration registration, CancellationToken ct)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperIdSpecification(shopperId), ct);

        if (investor is null)
        {
            // Claim the row before any provider call so a second concurrent sign-up is refused here.
            investor = await ClaimInvestorAsync(shopperId, ct);
        }
        else if (investor.UpvestUserId.HasValue)
        {
            // Already signed up: reconcile and return the existing investor (idempotent).
            return await ReconcileAsync(investor, ct);
        }

        // Step 1 — create the user. Required; if this fails the sign-up cannot proceed.
        if (!investor.UpvestUserId.HasValue)
        {
            var userId = await _gateway.CreateUserAsync(registration, investor.UserCreateKey, ct);
            investor.SetUpvestUser(userId);
            await _investors.UpdateAsync(investor, ct);
        }

        // Steps 2-5 — KYC, tax residency, account group, and the portfolio account. Each is
        // persisted as it succeeds. These require broader provider scopes; a failure here leaves the
        // investor 'pending' and is retried on the next GET, rather than failing the whole sign-up.
        await TryStepAsync("KYC check", ct, async () =>
        {
            if (!investor.UpvestKycCheckId.HasValue)
            {
                var checkId = await _gateway.CreateKycCheckAsync(investor.UpvestUserId!.Value, registration, ct);
                investor.SetKycCheck(checkId);
                await _investors.UpdateAsync(investor, ct);
            }
        });

        await TryStepAsync("tax residency", ct, async () =>
        {
            await _gateway.SetTaxResidencyAsync(investor.UpvestUserId!.Value, registration.TaxId, registration.TaxCountry, investor.TaxResidencyKey, ct);
        });

        await TryStepAsync("account group", ct, async () =>
        {
            if (!investor.UpvestAccountGroupId.HasValue)
            {
                var groupId = await _gateway.CreateAccountGroupAsync(investor.UpvestUserId!.Value, investor.AccountGroupCreateKey, ct);
                investor.SetAccountGroup(groupId);
                await _investors.UpdateAsync(investor, ct);
            }
        });

        await TryStepAsync("portfolio account", ct, async () =>
        {
            if (investor.UpvestAccountGroupId.HasValue && !investor.UpvestAccountId.HasValue)
            {
                var accountId = await _gateway.CreateAccountAsync(investor.UpvestUserId!.Value, investor.UpvestAccountGroupId.Value, investor.AccountCreateKey, ct);
                investor.SetAccount(accountId);
                await _investors.UpdateAsync(investor, ct);
            }
        });

        return await ReconcileAsync(investor, ct);
    }

    public async Task<InvestorResult?> GetAsync(string shopperId, CancellationToken ct)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperIdSpecification(shopperId), ct);
        if (investor is null || !investor.UpvestUserId.HasValue)
            return investor is null ? null : new InvestorResult(Guid.Empty, InvestorState.Pending);

        // Resume any provisioning that needs only the user id (so onboarding completes without a
        // second POST), then reconcile status from Upvest.
        await TryStepAsync("account group", ct, async () =>
        {
            if (!investor.UpvestAccountGroupId.HasValue)
            {
                var groupId = await _gateway.CreateAccountGroupAsync(investor.UpvestUserId!.Value, investor.AccountGroupCreateKey, ct);
                investor.SetAccountGroup(groupId);
                await _investors.UpdateAsync(investor, ct);
            }
        });
        await TryStepAsync("portfolio account", ct, async () =>
        {
            if (investor.UpvestAccountGroupId.HasValue && !investor.UpvestAccountId.HasValue)
            {
                var accountId = await _gateway.CreateAccountAsync(investor.UpvestUserId!.Value, investor.UpvestAccountGroupId.Value, investor.AccountCreateKey, ct);
                investor.SetAccount(accountId);
                await _investors.UpdateAsync(investor, ct);
            }
        });

        return await ReconcileAsync(investor, ct);
    }

    private async Task<Investor> ClaimInvestorAsync(string shopperId, CancellationToken ct)
    {
        try
        {
            var investor = new Investor(shopperId);
            return await _investors.AddAsync(investor, ct);
        }
        catch (Exception ex)
        {
            // A concurrent claim won the race; return the existing row rather than a second investor.
            _logger.LogWarning(ex, "Investor claim race for shopper; loading existing row.");
            var existing = await _investors.FirstOrDefaultAsync(new InvestorByShopperIdSpecification(shopperId), ct);
            if (existing is not null) return existing;
            throw;
        }
    }

    private async Task<InvestorResult> ReconcileAsync(Investor investor, CancellationToken ct)
    {
        var investorId = investor.UpvestUserId ?? Guid.Empty;
        try
        {
            var state = await _gateway.GetInvestorStateAsync(
                investor.UpvestUserId!.Value, investor.UpvestKycCheckId, investor.UpvestAccountGroupId, ct);
            investor.UpdateStatusCache(state.ToString().ToLowerInvariant());
            await _investors.UpdateAsync(investor, ct);
            return new InvestorResult(investorId, state);
        }
        catch (UpvestGatewayException ex)
        {
            // Could not read Upvest right now; return the last status learned from it.
            _logger.LogWarning(ex, "Could not read investor status from Upvest; using last-known status.");
            return new InvestorResult(investorId, ParseState(investor.StatusCache));
        }
    }

    private async Task TryStepAsync(string step, CancellationToken ct, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (UpvestGatewayException ex)
        {
            _logger.LogWarning(ex, "Onboarding step '{Step}' not yet completed; will retry on next read.", step);
        }
    }

    private static InvestorState ParseState(string cache) => cache switch
    {
        "active" => InvestorState.Active,
        "rejected" => InvestorState.Rejected,
        _ => InvestorState.Pending,
    };
}
