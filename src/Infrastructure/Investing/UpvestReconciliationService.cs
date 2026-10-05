using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.Infrastructure.Upvest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Reconciles the application's investing state with Upvest: finishes onboarding a shopper (checks, tax,
/// activation, holding account), invests an accumulated balance once it crosses the threshold, and settles
/// each investment to what actually happened to its order at Upvest. Runs continuously; every step is
/// idempotent and safe to repeat, and each shopper's data is only ever touched on their own behalf.
/// </summary>
public sealed class UpvestReconciliationService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InvestingCoordinator _coordinator;
    private readonly long _thresholdCents;
    private readonly ILogger<UpvestReconciliationService> _logger;

    public UpvestReconciliationService(
        IServiceScopeFactory scopeFactory,
        InvestingCoordinator coordinator,
        IOptions<UpvestOptions> options,
        ILogger<UpvestReconciliationService> logger)
    {
        _scopeFactory = scopeFactory;
        _coordinator = coordinator;
        _thresholdCents = (long)Math.Round(options.Value.InvestmentThresholdEuros * 100m, MidpointRounding.AwayFromZero);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Upvest reconciliation worker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, stoppingToken);
                await RunIterationAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upvest reconciliation iteration failed; continuing.");
            }
        }
    }

    private async Task RunIterationAsync(CancellationToken ct)
    {
        List<string> onboarding, investable;
        List<int> pendingInvestments;

        using (var scope = _scopeFactory.CreateScope())
        {
            var enrolments = scope.ServiceProvider.GetRequiredService<IRepository<Enrolment>>();
            var investments = scope.ServiceProvider.GetRequiredService<IRepository<Investment>>();
            onboarding = (await enrolments.ListAsync(new OnboardingEnrolmentsSpec(), ct)).Select(e => e.BuyerId).ToList();
            investable = (await enrolments.ListAsync(new InvestableEnrolmentsSpec(_thresholdCents), ct)).Select(e => e.BuyerId).ToList();
            pendingInvestments = (await investments.ListAsync(new PendingInvestmentsSpec(), ct)).Select(i => i.Id).ToList();
        }

        foreach (var buyerId in onboarding)
            await AdvanceOnboardingAsync(buyerId, ct);

        foreach (var buyerId in investable)
            await ClaimInvestmentAsync(buyerId, ct);

        foreach (var investmentId in pendingInvestments)
            await AdvanceInvestmentAsync(investmentId, ct);
    }

    // --- onboarding ---------------------------------------------------------------------------------

    private async Task AdvanceOnboardingAsync(string buyerId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var enrolments = scope.ServiceProvider.GetRequiredService<IRepository<Enrolment>>();
        var gateway = scope.ServiceProvider.GetRequiredService<IUpvestGateway>();

        var enrolment = await enrolments.FirstOrDefaultAsync(new EnrolmentByBuyerIdSpec(buyerId), ct);
        if (enrolment is null || enrolment.UpvestUserId is null)
            return; // user creation is owned by the enrol request (it has the form); nothing to do yet.

        var userId = enrolment.UpvestUserId;
        try
        {
            if (!enrolment.KycSubmitted)
            {
                await gateway.SubmitKycCheckAsync(userId, ct);
                await SaveEnrolmentAsync(buyerId, e => e.MarkKycSubmitted(), ct);
                return;
            }

            if (!enrolment.TaxSubmitted)
            {
                await gateway.SetTaxResidencyAsync(userId, enrolment.TaxCountry, enrolment.TaxId, enrolment.TaxIdempotencyKey, ct);
                await SaveEnrolmentAsync(buyerId, e => e.MarkTaxSubmitted(), ct);
                return;
            }

            if (enrolment.UpvestAccountGroupId is null)
            {
                var userStatus = await gateway.GetUserStatusAsync(userId, ct);
                if (!string.Equals(userStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                    return; // wait for Upvest to activate the user

                var groupId = await gateway.CreateAccountGroupAsync(userId, enrolment.AccountGroupIdempotencyKey, ct);
                await SaveEnrolmentAsync(buyerId, e => e.RecordAccountGroup(groupId), ct);
                return;
            }

            if (enrolment.UpvestAccountId is null)
            {
                var accountId = await gateway.CreateAccountAsync(userId, enrolment.UpvestAccountGroupId, enrolment.AccountIdempotencyKey, ct);
                await SaveEnrolmentAsync(buyerId, e => e.RecordAccount(accountId), ct);
                return;
            }

            var accountStatus = await gateway.GetAccountStatusAsync(enrolment.UpvestAccountId, ct);
            if (string.Equals(accountStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                await SaveEnrolmentAsync(buyerId, e => e.Activate(), ct);
                _logger.LogInformation("Enrolment {EnrolmentId} is now an accepted investor.", enrolment.Id);
            }
        }
        catch (UpvestApiException ex)
        {
            // A definitive client rejection (4xx that is not a "not yet ready" 409) stops onboarding.
            if (!ex.OutcomeUnknown && ex.StatusCode is >= 400 and < 500 and not 409)
            {
                await SaveEnrolmentAsync(buyerId, e => e.Reject($"Upvest declined onboarding (HTTP {ex.StatusCode})."), ct);
                _logger.LogWarning("Enrolment for buyer onboarding rejected by Upvest (HTTP {Status}).", ex.StatusCode);
            }
            else
            {
                _logger.LogWarning(ex, "Onboarding step deferred; will retry next iteration.");
            }
        }
    }

    // --- investing ----------------------------------------------------------------------------------

    private Task ClaimInvestmentAsync(string buyerId, CancellationToken ct) =>
        _coordinator.RunLockedAsync(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var enrolments = scope.ServiceProvider.GetRequiredService<IRepository<Enrolment>>();
            var investments = scope.ServiceProvider.GetRequiredService<IRepository<Investment>>();

            var enrolment = await enrolments.FirstOrDefaultAsync(new EnrolmentByBuyerIdSpec(buyerId), ct);
            if (enrolment is null || !enrolment.CanInvest || enrolment.PendingCents < _thresholdCents)
                return;

            var amount = enrolment.PendingCents; // invest the whole set-aside balance
            var investment = new Investment(enrolment.BuyerId, amount);
            enrolment.BeginInvestment(amount);

            await investments.AddAsync(investment, ct);
            await enrolments.UpdateAsync(enrolment, ct);
            _logger.LogInformation("Investment {InvestmentId} opened for {AmountCents} cents.", investment.Id, amount);
        }, ct);

    private async Task AdvanceInvestmentAsync(int investmentId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var enrolments = scope.ServiceProvider.GetRequiredService<IRepository<Enrolment>>();
        var investments = scope.ServiceProvider.GetRequiredService<IRepository<Investment>>();
        var gateway = scope.ServiceProvider.GetRequiredService<IUpvestGateway>();

        var investment = await investments.GetByIdAsync(investmentId, ct);
        if (investment is null || investment.Status != InvestmentStatus.Pending)
            return;

        var enrolment = await enrolments.FirstOrDefaultAsync(new EnrolmentByBuyerIdSpec(investment.BuyerId), ct);
        if (enrolment?.UpvestAccountGroupId is null || enrolment.UpvestAccountId is null || enrolment.UpvestUserId is null)
            return; // holding structure not ready yet

        try
        {
            if (!investment.Funded)
            {
                await gateway.FundAccountGroupAsync(enrolment.UpvestAccountGroupId, investment.AmountCents, investment.FundingIdempotencyKey, ct);
                await SaveInvestmentAsync(investmentId, i => i.MarkFunded(), ct);
                return;
            }

            if (investment.UpvestOrderId is null)
            {
                string orderId;
                try
                {
                    orderId = await gateway.PlaceBuyOrderAsync(
                        enrolment.UpvestUserId, enrolment.UpvestAccountId, investment.AmountCents,
                        investment.ClientReference, investment.OrderIdempotencyKey, ct);
                }
                catch (UpvestApiException ex) when (ex.OutcomeUnknown)
                {
                    // The order may or may not have been placed — reconcile by our client reference.
                    var found = await gateway.FindOrderByReferenceAsync(enrolment.UpvestAccountId, investment.ClientReference, ct);
                    if (found is null)
                    {
                        _logger.LogWarning("Order placement outcome unknown for investment {InvestmentId}; will reconcile.", investmentId);
                        return;
                    }
                    orderId = found.OrderId;
                }

                await SaveInvestmentAsync(investmentId, i => i.RecordOrder(orderId), ct);
                return;
            }

            var status = await gateway.GetOrderStatusAsync(investment.UpvestOrderId, ct);
            if (string.Equals(status, "FILLED", StringComparison.OrdinalIgnoreCase))
            {
                await SettleInvestmentAsync(investmentId, settled: true, ct);
                _logger.LogInformation("Investment {InvestmentId} settled.", investmentId);
            }
            else if (status is "CANCELLED" or "REJECTED" or "EXPIRED")
            {
                await SettleInvestmentAsync(investmentId, settled: false, ct);
                _logger.LogWarning("Investment {InvestmentId} failed at Upvest ({Status}); amount returned to the ledger.", investmentId, status);
            }
        }
        catch (UpvestApiException ex)
        {
            if (!ex.OutcomeUnknown && ex.StatusCode is >= 400 and < 500 and not 409)
            {
                await SettleInvestmentAsync(investmentId, settled: false, ct);
                _logger.LogWarning("Investment {InvestmentId} rejected by Upvest (HTTP {Status}); amount returned.", investmentId, ex.StatusCode);
            }
            else
            {
                _logger.LogWarning(ex, "Investment {InvestmentId} step deferred; will retry.", investmentId);
            }
        }
    }

    // --- locked ledger mutations (fresh scope each, so reads reflect concurrent set-asides) ----------

    private Task SaveEnrolmentAsync(string buyerId, Action<Enrolment> mutate, CancellationToken ct) =>
        _coordinator.RunLockedAsync(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var enrolments = scope.ServiceProvider.GetRequiredService<IRepository<Enrolment>>();
            var enrolment = await enrolments.FirstOrDefaultAsync(new EnrolmentByBuyerIdSpec(buyerId), ct);
            if (enrolment is null) return;
            mutate(enrolment);
            await enrolments.UpdateAsync(enrolment, ct);
        }, ct);

    private Task SaveInvestmentAsync(int investmentId, Action<Investment> mutate, CancellationToken ct) =>
        _coordinator.RunLockedAsync(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var investments = scope.ServiceProvider.GetRequiredService<IRepository<Investment>>();
            var investment = await investments.GetByIdAsync(investmentId, ct);
            if (investment is null) return;
            mutate(investment);
            await investments.UpdateAsync(investment, ct);
        }, ct);

    private Task SettleInvestmentAsync(int investmentId, bool settled, CancellationToken ct) =>
        _coordinator.RunLockedAsync(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var enrolments = scope.ServiceProvider.GetRequiredService<IRepository<Enrolment>>();
            var investments = scope.ServiceProvider.GetRequiredService<IRepository<Investment>>();

            var investment = await investments.GetByIdAsync(investmentId, ct);
            if (investment is null || investment.Status != InvestmentStatus.Pending) return;

            var enrolment = await enrolments.FirstOrDefaultAsync(new EnrolmentByBuyerIdSpec(investment.BuyerId), ct);
            if (settled)
            {
                investment.Settle();
                enrolment?.SettleInvestment(investment.AmountCents);
            }
            else
            {
                investment.Fail();
                enrolment?.FailInvestment(investment.AmountCents); // money returns to the pending ledger
            }

            await investments.UpdateAsync(investment, ct);
            if (enrolment is not null)
                await enrolments.UpdateAsync(enrolment, ct);
        }, ct);
}
