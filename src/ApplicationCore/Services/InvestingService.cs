using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class InvestingService : IInvestingService
{
    /// <summary>Euros that must accrue before the set-aside balance is invested.</summary>
    public const decimal InvestmentThreshold = 10m;

    private readonly IRepository<Investor> _investors;
    private readonly IUpvestGateway _upvest;
    private readonly IInvestorMutationGate _gate;
    private readonly IAppLogger<InvestingService> _logger;

    public InvestingService(
        IRepository<Investor> investors,
        IUpvestGateway upvest,
        IInvestorMutationGate gate,
        IAppLogger<InvestingService> logger)
    {
        _investors = investors;
        _upvest = upvest;
        _gate = gate;
        _logger = logger;
    }

    public async Task<Investor> EnrolAsync(string shopperId, InvestorEnrolmentDetails details, CancellationToken cancellationToken = default)
    {
        var existing = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), cancellationToken);
        if (existing is not null)
        {
            // Opting in is idempotent: a shopper already enrolled keeps their enrolment.
            return existing;
        }

        var result = await _upvest.EnrolInvestorAsync(details, cancellationToken);

        var investor = new Investor(shopperId, result.UserId);
        if (!string.IsNullOrEmpty(result.AccountGroupId) && !string.IsNullOrEmpty(result.AccountId))
        {
            investor.SetAccounts(result.AccountGroupId, result.AccountId);
        }
        investor.SetKycCheck(result.CheckId);

        await _investors.AddAsync(investor, cancellationToken);
        _logger.LogInformation("Enrolled shopper into investing (enrolment {EnrolmentId}).", investor.PublicId);
        return investor;
    }

    public Task<Investor?> GetEnrolmentAsync(string shopperId, CancellationToken cancellationToken = default)
        => _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), cancellationToken);

    public async Task<decimal> ProcessPaidOrderAsync(string shopperId, decimal orderTotal, CancellationToken cancellationToken = default)
    {
        try
        {
            // Serialise with the reconciler/webhook writers so the set-aside is not lost.
            return await _gate.RunAsync(async () =>
            {
                var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), cancellationToken);
                if (investor is null) return 0m;

                var setAside = investor.SetAsideChange(orderTotal);
                if (setAside <= 0m) return 0m;

                // Only the round-up is set aside here; the actual investment, once the balance
                // reaches the threshold, is placed out-of-band by the reconciler so that placing an
                // order never depends on a call to Upvest.
                await _investors.UpdateAsync(investor, cancellationToken);
                return setAside;
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            // Placing an order must never fail because of anything to do with investing.
            _logger.LogWarning("Setting aside change for a paid order failed ({Error}); the order itself is unaffected.", ex.Message);
            return 0m;
        }
    }

    public async Task<IReadOnlyList<Investment>> GetInvestmentsAsync(string shopperId, CancellationToken cancellationToken = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), cancellationToken);
        if (investor is null) return Array.Empty<Investment>();

        return investor.Investments
            .OrderByDescending(i => i.CreatedAt)
            .ToList();
    }
}
