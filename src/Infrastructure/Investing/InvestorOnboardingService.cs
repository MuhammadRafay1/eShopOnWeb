using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.Infrastructure.Investing.Http;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Enrols a shopper with Upvest following the Take-Our-License onboarding flow: create the user,
/// submit the KYC and instrument-fit checks, declare tax residency, then create the account group
/// and a trading account. The enrolment record is created first and left <c>Pending</c>; Upvest
/// accepts the shopper asynchronously (surfaced later by the reconciliation sweep / webhooks).
/// </summary>
public sealed class InvestorOnboardingService : IInvestorOnboardingService
{
    private readonly IRepository<InvestingAccount> _repository;
    private readonly IUpvestInvestmentClient _upvest;
    private readonly InvestingConcurrencyGuard _guard;
    private readonly ILogger<InvestorOnboardingService> _logger;

    public InvestorOnboardingService(
        IRepository<InvestingAccount> repository,
        IUpvestInvestmentClient upvest,
        InvestingConcurrencyGuard guard,
        ILogger<InvestorOnboardingService> logger)
    {
        _repository = repository;
        _upvest = upvest;
        _guard = guard;
        _logger = logger;
    }

    public async Task<InvestingAccount> EnrolAsync(string buyerId, InvestorRegistration registration, CancellationToken cancellationToken = default)
    {
        InvestingAccount account;
        using (await _guard.AcquireAsync(cancellationToken))
        {
            var existing = await _repository.FirstOrDefaultAsync(new InvestingAccountByBuyerSpec(buyerId), cancellationToken);
            if (existing is not null)
            {
                // Enrolment is idempotent per shopper. Only re-run onboarding if a previous attempt
                // left it incomplete (no Upvest user was created); otherwise return it unchanged.
                if (existing.Status != EnrolmentStatus.Pending || !string.IsNullOrEmpty(existing.UpvestUserId))
                {
                    return existing;
                }
                account = existing;
            }
            else
            {
                account = new InvestingAccount(buyerId);
                await _repository.AddAsync(account, cancellationToken);
            }
        }

        try
        {
            var user = await _upvest.CreateUserAsync(registration, cancellationToken);
            await UpdateAsync(account, a => a.LinkUpvestUser(user.Id), cancellationToken);

            await _upvest.SubmitKycCheckAsync(user.Id, registration, cancellationToken);
            await _upvest.SubmitInstrumentFitCheckAsync(user.Id, cancellationToken);
            await _upvest.SetTaxResidenciesAsync(user.Id, registration.TaxCountry, registration.TaxId, cancellationToken);

            // The account group and trading account are created once Upvest has activated the user
            // (the reconciliation sweep does this); creating them earlier is rejected while the
            // user's checks are still being processed.
            _logger.LogInformation("Investor onboarding submitted for enrolment {EnrolmentId}; awaiting acceptance.", account.EnrolmentId);
        }
        catch (Exception ex)
        {
            // The enrolment record still exists (Pending); never rethrow so the caller gets a
            // consistent pending enrolment. The sweep will keep it pending until it can complete.
            _logger.LogError(ex, "Investor onboarding did not complete for enrolment {EnrolmentId}.", account.EnrolmentId);
        }

        return account;
    }

    private async Task UpdateAsync(InvestingAccount account, Action<InvestingAccount> mutate, CancellationToken cancellationToken)
    {
        using (await _guard.AcquireAsync(cancellationToken))
        {
            mutate(account);
            await _repository.UpdateAsync(account, cancellationToken);
        }
    }
}
