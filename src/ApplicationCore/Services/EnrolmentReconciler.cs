using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class EnrolmentReconciler : IEnrolmentReconciler
{
    private static readonly string[] RejectionStatuses = { "REJECTED", "BLOCKED", "CLOSED", "DECLINED" };

    private readonly IRepository<Investor> _investors;
    private readonly IUpvestGateway _upvest;
    private readonly IAppLogger<EnrolmentReconciler> _logger;

    public EnrolmentReconciler(IRepository<Investor> investors, IUpvestGateway upvest, IAppLogger<EnrolmentReconciler> logger)
    {
        _investors = investors;
        _upvest = upvest;
        _logger = logger;
    }

    public async Task ReconcileAsync(Investor investor, CancellationToken cancellationToken)
    {
        if (investor.UpvestUserId is null)
        {
            return;
        }

        var dirty = false;
        try
        {
            if (investor.Status == EnrolmentStatus.Pending)
            {
                var userStatus = await _upvest.GetUserStatusAsync(investor.UpvestUserId, cancellationToken);
                if (string.Equals(userStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                {
                    investor.MarkActive();
                    dirty = true;
                    _logger.LogInformation("Investor {InvestorId} accepted by Upvest.", investor.Id);
                }
                else if (Array.Exists(RejectionStatuses, s => s.Equals(userStatus, StringComparison.OrdinalIgnoreCase)))
                {
                    investor.MarkRejected();
                    dirty = true;
                    _logger.LogInformation("Investor {InvestorId} rejected by Upvest.", investor.Id);
                }
            }

            if (investor.Status == EnrolmentStatus.Active && investor.AccountId is null)
            {
                var setup = await _upvest.CreateAccountSetupAsync(
                    investor.UpvestUserId!, investor.GroupIdempotencyKey, investor.AccountIdempotencyKey, cancellationToken);
                investor.LinkAccount(setup.AccountGroupId, setup.AccountId);
                if (string.Equals(setup.AccountStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                {
                    investor.MarkAccountActive();
                }
                dirty = true;
                _logger.LogInformation("Investor {InvestorId} account created.", investor.Id);
            }

            if (investor.Status == EnrolmentStatus.Active && investor.AccountId is not null && !investor.AccountActive)
            {
                var accountStatus = await _upvest.GetAccountStatusAsync(investor.AccountId, cancellationToken);
                if (string.Equals(accountStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                {
                    investor.MarkAccountActive();
                    dirty = true;
                    _logger.LogInformation("Investor {InvestorId} account is now active.", investor.Id);
                }
            }
        }
        catch (UpvestException ex)
        {
            // Reconciliation is best-effort: leave the state as-is and try again on the next read/order.
            _logger.LogWarning("Reconciliation for investor {InvestorId} deferred: {Reason}", investor.Id, ex.Message);
        }

        if (dirty)
        {
            await _investors.UpdateAsync(investor, cancellationToken);
        }
    }
}
