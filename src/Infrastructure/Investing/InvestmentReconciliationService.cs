using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Converges local state with Upvest by polling: finishes pending enrolments as
/// users and accounts activate, invests balances that have crossed the
/// threshold, and settles investments as their orders complete. This is the
/// reliable path; inbound webhooks simply trigger a sweep sooner.
/// </summary>
public sealed class InvestmentReconciliationService : IInvestmentReconciliationService
{
    private readonly CatalogContext _db;
    private readonly IUpvestClient _upvest;
    private readonly IInvestingService _investing;
    private readonly ILogger<InvestmentReconciliationService> _logger;

    public InvestmentReconciliationService(
        CatalogContext db,
        IUpvestClient upvest,
        IInvestingService investing,
        ILogger<InvestmentReconciliationService> logger)
    {
        _db = db;
        _upvest = upvest;
        _investing = investing;
        _logger = logger;
    }

    public async Task ReconcileAsync(CancellationToken ct)
    {
        await AdvancePendingEnrolmentsAsync(ct);
        await InvestReadyBalancesAsync(ct);
        await SettleInvestmentsAsync(ct);
    }

    private async Task AdvancePendingEnrolmentsAsync(CancellationToken ct)
    {
        var pending = await _db.Investors
            .Where(i => i.Status == EnrolmentStatus.Pending && i.UpvestUserId != null)
            .ToListAsync(ct);

        foreach (var investor in pending)
        {
            try
            {
                var user = await _upvest.GetUserAsync(investor.UpvestUserId!, ct);
                if (!IsActive(user.Status))
                {
                    continue;
                }

                if (investor.UpvestAccountGroupId is null)
                {
                    var group = await _upvest.CreateAccountGroupAsync(investor.UpvestUserId!, ct);
                    investor.LinkAccountGroup(group.Id);
                }
                else if (investor.UpvestAccountId is null)
                {
                    var account = await _upvest.CreateAccountAsync(investor.UpvestUserId!, investor.UpvestAccountGroupId, ct);
                    investor.LinkAccount(account.Id);
                }
                else
                {
                    var account = await _upvest.GetAccountAsync(investor.UpvestAccountId, ct);
                    if (IsActive(account.Status))
                    {
                        investor.Activate();
                        _logger.LogInformation("Enrolment {InvestorId} accepted by Upvest; now active.", investor.Id);
                    }
                }

                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Advancing enrolment {InvestorId} failed; will retry.", investor.Id);
            }
        }
    }

    private async Task InvestReadyBalancesAsync(CancellationToken ct)
    {
        var ready = await _db.Investors
            .Include(i => i.Investments)
            .Where(i => i.Status == EnrolmentStatus.Active && i.PendingAmount >= Investor.InvestmentThresholdEuros)
            .ToListAsync(ct);

        foreach (var investor in ready)
        {
            try
            {
                await _investing.TryInvestAsync(investor, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Investing balance for enrolment {InvestorId} failed; will retry.", investor.Id);
            }
        }
    }

    private async Task SettleInvestmentsAsync(CancellationToken ct)
    {
        var open = await _db.Set<Investment>()
            .Where(x => x.Status == InvestmentStatus.Pending)
            .ToListAsync(ct);

        foreach (var investment in open)
        {
            try
            {
                var order = await _upvest.GetOrderAsync(investment.UpvestOrderId, ct);
                if (string.Equals(order.Status, "FILLED", StringComparison.OrdinalIgnoreCase))
                {
                    investment.MarkSettled();
                    await _db.SaveChangesAsync(ct);
                    _logger.LogInformation("Investment {InvestmentId} settled.", investment.Id);
                }
                else if (string.Equals(order.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
                {
                    investment.MarkFailed();
                    await _db.SaveChangesAsync(ct);
                    _logger.LogInformation("Investment {InvestmentId} failed at Upvest.", investment.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Settling investment {InvestmentId} failed; will retry.", investment.Id);
            }
        }
    }

    private static bool IsActive(string status) => string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase);
}
