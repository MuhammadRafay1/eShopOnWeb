using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class InvestorService : IInvestorService
{
    private readonly IRepository<Investor> _investors;
    private readonly IUpvestClient _upvest;
    private readonly IAppLogger<InvestorService> _logger;

    // Nationalities for which Upvest auto-generates the regulatory identifier (CONCAT) — no identifier call needed.
    private static readonly HashSet<string> ConcatNationalities =
        new(StringComparer.OrdinalIgnoreCase) { "AT", "DE", "FR", "HU", "IE", "LU" };

    public InvestorService(IRepository<Investor> investors, IUpvestClient upvest, IAppLogger<InvestorService> logger)
    {
        _investors = investors;
        _upvest = upvest;
        _logger = logger;
    }

    public async Task<Investor> SignUpAsync(string shopUserName, InvestorSignup signup, CancellationToken ct = default)
    {
        Guard.Against.NullOrEmpty(shopUserName, nameof(shopUserName));
        Guard.Against.Null(signup, nameof(signup));

        var existing = await _investors.FirstOrDefaultAsync(new InvestorByShopUserSpecification(shopUserName), ct);
        if (existing is not null)
            return existing; // Idempotent: a shopper signs up once.

        var investor = new Investor(shopUserName);

        // 1. Create the Upvest user (mandatory — this is the shopper becoming an investor).
        var user = await _upvest.CreateUserAsync(signup, ct);
        investor.SetUpvestUser(user.Id, user.Status);
        _logger.LogInformation("Created Upvest user for investor sign-up (status {Status}).", user.Status ?? "unknown");

        // 2. Regulatory identifier (skipped for CONCAT nationalities; best-effort otherwise).
        if (!ConcatNationalities.Contains(signup.Nationality))
            await BestEffort("create identifier",
                () => _upvest.CreateUserIdentifierAsync(user.Id, signup.IdentityDocument, ct));

        // 3. Submit KYC check (async at Upvest; best-effort).
        await BestEffort("submit KYC check",
            () => _upvest.CreateKycCheckAsync(user.Id, signup, ct));

        // 4. Tax residency (best-effort).
        await BestEffort("set tax residencies",
            () => _upvest.SetTaxResidenciesAsync(user.Id, signup.TaxCountry, signup.TaxId, ct));

        // 5. Account group, then 6. a PORTFOLIO account to hold the managed portfolio (best-effort).
        var group = await BestEffort("create account group",
            () => _upvest.CreateAccountGroupAsync(user.Id, ct));
        if (group is not null)
        {
            investor.SetAccountGroup(group.Id, group.Status);
            var account = await BestEffort("create account",
                () => _upvest.CreateAccountAsync(user.Id, group.Id, ct));
            if (account is not null)
                investor.SetAccount(account.Id, account.Status);
        }

        await _investors.AddAsync(investor, ct);
        return investor;
    }

    public async Task<Investor?> GetAsync(string shopUserName, CancellationToken ct = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopUserSpecification(shopUserName), ct);
        if (investor is null) return null;

        // Learn the real status from Upvest through our own calls (no webhooks).
        bool changed = false;

        if (investor.UpvestUserId is not null)
        {
            try
            {
                var user = await _upvest.GetUserAsync(investor.UpvestUserId, ct);
                if (user?.Status is not null && user.Status != investor.UpvestUserStatus)
                {
                    investor.UpdateStatuses(user.Status, null, null);
                    changed = true;
                }
            }
            catch (UpvestException ex)
            {
                _logger.LogWarning("Could not refresh user status (HTTP {Status}: {Detail}).", ex.StatusCode, ex.Message);
            }
        }

        if (investor.AccountId is not null)
        {
            try
            {
                var account = await _upvest.GetAccountAsync(investor.AccountId, ct);
                if (account?.Status is not null && account.Status != investor.AccountStatus)
                {
                    investor.UpdateStatuses(null, null, account.Status);
                    changed = true;
                }
            }
            catch (UpvestException ex)
            {
                _logger.LogWarning("Could not refresh account status (HTTP {Status}: {Detail}).", ex.StatusCode, ex.Message);
            }
        }

        if (changed) await _investors.UpdateAsync(investor, ct);
        return investor;
    }

    // Swallows scope-authorisation failures (the known sandbox limitation) and other Upvest errors on
    // non-critical onboarding steps, logging a non-personal note so sign-up can proceed. Onboarding at
    // Upvest is asynchronous; the final status is learned later via polling.
    private async Task<T?> BestEffort<T>(string step, Func<Task<T>> action) where T : class
    {
        try
        {
            return await action();
        }
        catch (UpvestException ex)
        {
            _logger.LogWarning("Upvest step '{Step}' did not complete now (HTTP {Status}): {Detail}.",
                step, ex.StatusCode, ex.Message);
            return null;
        }
    }

    private async Task BestEffort(string step, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (UpvestException ex)
        {
            _logger.LogWarning("Upvest step '{Step}' did not complete now (HTTP {Status}): {Detail}.",
                step, ex.StatusCode, ex.Message);
        }
    }
}
