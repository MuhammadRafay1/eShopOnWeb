using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services.Portfolio;

public class PortfolioInvestorService : IPortfolioInvestorService
{
    private readonly IRepository<Investor> _investors;
    private readonly IUpvestInvestmentGateway _gateway;
    private readonly IAppLogger<PortfolioInvestorService> _logger;

    public PortfolioInvestorService(
        IRepository<Investor> investors,
        IUpvestInvestmentGateway gateway,
        IAppLogger<PortfolioInvestorService> logger)
    {
        _investors = investors;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<InvestorView> SignUpAsync(string shopperUserName, InvestorSignup signup, CancellationToken ct)
    {
        // Duplicate-claim: one investor record per shopper, keyed by user name. A second concurrent
        // sign-up is refused by the unique key (SQL Server) / rejected on save; here an existing record
        // with an established Upvest user is returned as-is rather than creating a second one.
        var existing = await _investors.FirstOrDefaultAsync(new InvestorByUserNameSpecification(shopperUserName), ct);

        if (existing is not null && existing.UpvestUserId is not null)
        {
            // Already an investor — report current (reconciled) status; never create twice.
            return await ReconcileAsync(existing, ct);
        }

        var investor = existing;
        if (investor is null)
        {
            investor = new Investor(shopperUserName);
            investor = await _investors.AddAsync(investor, ct); // claim, before any Upvest call
        }

        var keys = new InvestorProvisioningKeys(
            investor.UserIdempotencyKey,
            investor.AccountGroupIdempotencyKey,
            investor.AccountIdempotencyKey);

        try
        {
            var provisioning = await _gateway.CreateInvestorAsync(signup, keys, ct);
            investor.SetUpvestUser(provisioning.UserId);
            if (provisioning.AccountGroupId is not null && provisioning.AccountId is not null)
            {
                investor.SetUpvestAccount(provisioning.AccountGroupId.Value, provisioning.AccountId.Value);
            }
            else if (provisioning.AccountGroupId is not null)
            {
                investor.SetAccountGroup(provisioning.AccountGroupId.Value);
            }
            investor.SetStatus(provisioning.Status);
            await _investors.UpdateAsync(investor, ct);

            _logger.LogInformation(
                "Investor sign-up for shopper processed; upvest user {UserId}, status {Status}.",
                provisioning.UserId, provisioning.Status);

            return new InvestorView(provisioning.UserId, provisioning.Status);
        }
        catch (UpvestGatewayException ex)
        {
            // The record stays as a pending claim so a re-POST (with the form) can retry provisioning
            // with the same idempotency keys. Nothing is deleted: the claim both dedupes and enables retry.
            await _investors.UpdateAsync(investor, ct);
            _logger.LogWarning(
                "Investor sign-up could not complete (status {Status}); left pending for retry.",
                ex.StatusCode);
            throw;
        }
    }

    public async Task<InvestorView?> GetStatusAsync(string shopperUserName, CancellationToken ct)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByUserNameSpecification(shopperUserName), ct);
        if (investor is null)
        {
            return null;
        }

        if (investor.UpvestUserId is null)
        {
            // Sign-up never established a user; report pending without an id.
            return new InvestorView(Guid.Empty, investor.Status);
        }

        return await ReconcileAsync(investor, ct);
    }

    /// <summary>
    /// Re-read (and, where possible, self-heal) an investor's state against Upvest, then persist and return
    /// it. Reads must not fail the caller: if Upvest cannot be reached (e.g. the sandbox scope wall), the
    /// last-known status is returned.
    /// </summary>
    private async Task<InvestorView> ReconcileAsync(Investor investor, CancellationToken ct)
    {
        var userId = investor.UpvestUserId!.Value;
        try
        {
            if (!investor.AccountProvisioned)
            {
                var keys = new InvestorProvisioningKeys(
                    investor.UserIdempotencyKey,
                    investor.AccountGroupIdempotencyKey,
                    investor.AccountIdempotencyKey);
                var provisioning = await _gateway.ProvisionAccountAsync(
                    userId, investor.UpvestAccountGroupId, investor.UpvestAccountId, keys, ct);
                if (provisioning.AccountGroupId is not null && provisioning.AccountId is not null)
                {
                    investor.SetUpvestAccount(provisioning.AccountGroupId.Value, provisioning.AccountId.Value);
                }
                else if (provisioning.AccountGroupId is not null)
                {
                    investor.SetAccountGroup(provisioning.AccountGroupId.Value);
                }
                investor.SetStatus(provisioning.Status);
            }
            else
            {
                var status = await _gateway.GetInvestorStatusAsync(
                    userId, investor.UpvestAccountGroupId, investor.UpvestAccountId, ct);
                investor.SetStatus(status.Status);
            }

            await _investors.UpdateAsync(investor, ct);
        }
        catch (UpvestGatewayException ex)
        {
            _logger.LogWarning(
                "Could not reconcile investor status against Upvest (status {Status}); returning last-known {Known}.",
                ex.StatusCode, investor.Status);
        }

        return new InvestorView(userId, investor.Status);
    }
}
