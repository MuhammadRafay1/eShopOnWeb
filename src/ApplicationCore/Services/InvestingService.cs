using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class InvestingService : IInvestingService
{
    private readonly IRepository<Investor> _investors;
    private readonly IUpvestInvestorGateway _upvest;
    private readonly IAppLogger<InvestingService> _logger;

    public InvestingService(
        IRepository<Investor> investors,
        IUpvestInvestorGateway upvest,
        IAppLogger<InvestingService> logger)
    {
        _investors = investors;
        _upvest = upvest;
        _logger = logger;
    }

    public async Task<Investor> EnrolAsync(string shopperId, EnrolmentForm form, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));
        Guard.Against.Null(form, nameof(form));

        var existing = await _investors.FirstOrDefaultAsync(
            new InvestorByShopperIdSpecification(shopperId), cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var upvestUserId = await _upvest.EnrolInvestorAsync(form, cancellationToken);
        var investor = new Investor(shopperId, upvestUserId);
        await _investors.AddAsync(investor, cancellationToken);
        _logger.LogInformation($"Investor enrolled for shopper (enrolment {investor.Id}).");
        return investor;
    }

    public async Task<Investor?> GetEnrolmentAsync(string shopperId, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));

        var investor = await _investors.FirstOrDefaultAsync(
            new InvestorByShopperIdSpecification(shopperId), cancellationToken);
        if (investor is null) return null;

        if (investor.Status == InvestorStatus.Pending)
        {
            await RefreshAcceptanceAsync(investor, cancellationToken);
        }

        return investor;
    }

    public async Task<Investor?> GetInvestorAsync(string shopperId, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));

        var investor = await _investors.FirstOrDefaultAsync(
            new InvestorByShopperIdSpecification(shopperId, includeInvestments: true), cancellationToken);
        if (investor is null) return null;

        // Keep acceptance fresh so the balance view reflects reality.
        if (investor.Status == InvestorStatus.Pending)
        {
            await RefreshAcceptanceAsync(investor, cancellationToken);
        }

        await SettlePendingInvestmentsAsync(investor, cancellationToken);
        return investor;
    }

    public async Task<long> HandlePaidOrderAsync(string shopperId, decimal orderTotalInEuros, CancellationToken cancellationToken)
    {
        long setAside = 0;
        try
        {
            if (string.IsNullOrEmpty(shopperId)) return 0;

            var investor = await _investors.FirstOrDefaultAsync(
                new InvestorByShopperIdSpecification(shopperId, includeInvestments: true), cancellationToken);

            // Orders from a shopper who is not an accepted investor set nothing aside.
            if (investor is null || !investor.IsAccepted) return 0;

            var roundUp = RoundUpInCents(orderTotalInEuros);
            if (roundUp == 0) return 0;

            var reachedThreshold = investor.SetAsideChange(roundUp);
            await _investors.UpdateAsync(investor, cancellationToken);
            setAside = roundUp;

            if (reachedThreshold)
            {
                await TryInvestBalanceAsync(investor, cancellationToken);
            }

            return setAside;
        }
        catch (Exception ex)
        {
            // Placing an order must never fail because of anything to do with investing.
            _logger.LogWarning($"Setting aside change failed after the order was placed: {ex.Message}");
            return setAside;
        }
    }

    public async Task ReconcileAsync(Guid upvestId, CancellationToken cancellationToken)
    {
        if (upvestId == Guid.Empty) return;

        var investor = await _investors.FirstOrDefaultAsync(
            new InvestorByUpvestIdSpecification(upvestId), cancellationToken);
        if (investor is null) return;

        if (investor.Status == InvestorStatus.Pending)
        {
            await RefreshAcceptanceAsync(investor, cancellationToken);
        }

        await SettlePendingInvestmentsAsync(investor, cancellationToken);
    }

    private async Task TryInvestBalanceAsync(Investor investor, CancellationToken cancellationToken)
    {
        // Resolve the account that will hold the investment, if we do not have it yet.
        if (investor.UpvestAccountId is null)
        {
            Guid? accountId;
            try
            {
                accountId = await _upvest.TryResolveInvestmentAccountAsync(investor.UpvestUserId, cancellationToken);
            }
            catch (UpvestIntegrationException ex)
            {
                _logger.LogWarning($"Could not resolve an investment account yet: {ex.Message}. Change stays set aside.");
                return;
            }

            if (accountId is null)
            {
                _logger.LogInformation("No investment account available yet; change stays set aside for the next attempt.");
                return;
            }

            investor.SetInvestmentAccount(accountId.Value);
            await _investors.UpdateAsync(investor, cancellationToken);
        }

        // Move the whole balance into a tranche and persist it (with its idempotency key) BEFORE the call,
        // so a retry reuses the same key and the balance cannot be invested twice.
        var tranche = investor.BeginInvestment();
        await _investors.UpdateAsync(investor, cancellationToken);

        var amountEuros = tranche.AmountInCents / 100m;
        try
        {
            var placement = await _upvest.PlaceInvestmentAsync(
                investor.UpvestAccountId!.Value, amountEuros, tranche.IdempotencyKey, cancellationToken);

            tranche.LinkToUpvestOrder(placement.OrderId);
            switch (placement.Outcome)
            {
                case InvestmentStatus.Settled:
                    tranche.MarkSettled();
                    break;
                case InvestmentStatus.Failed:
                    investor.FailInvestment(tranche);
                    break;
            }
            await _investors.UpdateAsync(investor, cancellationToken);
            _logger.LogInformation($"Invested {amountEuros:0.00} EUR (tranche {tranche.Id}, status {tranche.Status}).");
        }
        catch (UpvestIntegrationException ex) when (ex.IsClientError)
        {
            // The provider definitively rejected the order; return the money to the set-aside balance.
            investor.FailInvestment(tranche);
            await _investors.UpdateAsync(investor, cancellationToken);
            _logger.LogWarning($"Investment rejected by provider (HTTP {(int?)ex.StatusCode}); change returned to balance.");
        }
        catch (UpvestIntegrationException ex)
        {
            // Unknown outcome (transport/5xx): the order may have landed. Leave the tranche pending so
            // settlement reconciles it against the provider later.
            _logger.LogWarning($"Investment outcome unknown ({ex.Message}); tranche {tranche.Id} left pending for reconciliation.");
            await _investors.UpdateAsync(investor, cancellationToken);
        }
    }

    private async Task RefreshAcceptanceAsync(Investor investor, CancellationToken cancellationToken)
    {
        try
        {
            var status = await _upvest.GetAcceptanceStatusAsync(investor.UpvestUserId, cancellationToken);
            if (status == InvestorStatus.Active && investor.Status != InvestorStatus.Active)
            {
                investor.MarkAccepted();
                await _investors.UpdateAsync(investor, cancellationToken);
            }
            else if (status == InvestorStatus.Rejected && investor.Status != InvestorStatus.Rejected)
            {
                investor.MarkRejected();
                await _investors.UpdateAsync(investor, cancellationToken);
            }
        }
        catch (UpvestIntegrationException ex)
        {
            // A transient read failure must not break the enrolment view; status simply stays pending.
            _logger.LogWarning($"Could not refresh acceptance status: {ex.Message}");
        }
    }

    private async Task SettlePendingInvestmentsAsync(Investor investor, CancellationToken cancellationToken)
    {
        var pending = investor.Investments
            .Where(i => i.Status == InvestmentStatus.Pending && i.UpvestOrderId.HasValue)
            .ToList();
        if (pending.Count == 0) return;

        var changed = false;
        foreach (var tranche in pending)
        {
            try
            {
                var outcome = await _upvest.GetInvestmentOutcomeAsync(tranche.UpvestOrderId!.Value, cancellationToken);
                if (outcome == InvestmentStatus.Settled)
                {
                    tranche.MarkSettled();
                    changed = true;
                }
                else if (outcome == InvestmentStatus.Failed)
                {
                    investor.FailInvestment(tranche);
                    changed = true;
                }
            }
            catch (UpvestIntegrationException ex)
            {
                _logger.LogWarning($"Could not settle investment {tranche.Id}: {ex.Message}");
            }
        }

        if (changed)
        {
            await _investors.UpdateAsync(investor, cancellationToken);
        }
    }

    /// <summary>
    /// The change set aside by an order: the difference to the next whole euro. €12.30 → 70 cents;
    /// a whole number of euros → 0.
    /// </summary>
    internal static long RoundUpInCents(decimal orderTotalInEuros)
    {
        if (orderTotalInEuros <= 0) return 0;
        var totalCents = (long)Math.Round(orderTotalInEuros * 100m, MidpointRounding.AwayFromZero);
        var remainder = totalCents % 100;
        return remainder == 0 ? 0 : 100 - remainder;
    }
}
