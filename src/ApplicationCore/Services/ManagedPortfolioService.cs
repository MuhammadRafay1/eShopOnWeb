using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.ApplicationCore.Upvest;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Drives the managed-portfolio flows end to end, keeping local correlation records aligned with
/// Upvest. Every status it reports is derived from what Upvest actually reports. Where the Upvest
/// sandbox refuses a call for lack of a scope, onboarding/provisioning steps degrade gracefully:
/// the local record is kept so the flow stays invocable, and the shortfall is logged without any
/// personal detail.
/// </summary>
public class ManagedPortfolioService : IManagedPortfolioService
{
    private const string EuroCurrency = "EUR";

    private readonly IRepository<Investor> _investors;
    private readonly IRepository<ModelPortfolio> _models;
    private readonly IRepository<PortfolioPayment> _payments;
    private readonly IRepository<Realignment> _realignments;
    private readonly IUpvestInvestmentService _upvest;
    private readonly IAppLogger<ManagedPortfolioService> _logger;

    public ManagedPortfolioService(
        IRepository<Investor> investors,
        IRepository<ModelPortfolio> models,
        IRepository<PortfolioPayment> payments,
        IRepository<Realignment> realignments,
        IUpvestInvestmentService upvest,
        IAppLogger<ManagedPortfolioService> logger)
    {
        _investors = investors;
        _models = models;
        _payments = payments;
        _realignments = realignments;
        _upvest = upvest;
        _logger = logger;
    }

    public async Task<Investor> SignUpInvestorAsync(string shopperId, InvestorSignUp signUp, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));
        Guard.Against.Null(signUp, nameof(signUp));

        var existing = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), cancellationToken);
        if (existing is not null)
        {
            throw new DuplicateInvestorException();
        }

        var investor = new Investor(shopperId, signUp.FirstName, signUp.LastName);

        // Step 1 (critical): create the user at Upvest.
        var registration = new UpvestUserRegistration(
            signUp.FirstName, signUp.LastName, signUp.Email, signUp.BirthDate, signUp.BirthCity,
            signUp.BirthCountry, signUp.Nationality,
            new UpvestAddress(signUp.Address.Line1, signUp.Address.Postcode, signUp.Address.City, signUp.Address.Country),
            signUp.PhoneNumber);

        var user = await _upvest.CreateUserAsync(registration, cancellationToken);
        investor.LinkUpvestUser(user.Id);

        // Step 2+ (best-effort): tax residency, identity check, account group and portfolio account.
        // In the sandbox these need scopes the client does not have; the investor then stays pending.
        var confirmedAddress = new UpvestAddress(signUp.Address.Line1, signUp.Address.Postcode, signUp.Address.City, signUp.Address.Country);

        var taxOk = await TryAsync("set-tax-residencies", () =>
            _upvest.SetTaxResidenciesAsync(user.Id, new[] { new UpvestTaxResidency(signUp.TaxCountry, signUp.TaxId) }, cancellationToken));
        var kycId = await TryAsync("create-kyc-check", () => _upvest.CreateKycCheckAsync(user.Id, new UpvestKycCheck(
            signUp.IdentityDocument.Type, signUp.IdentityDocument.Number, signUp.IdentityDocument.IssuingCountry,
            signUp.IdentityDocument.ExpiryDate, confirmedAddress), cancellationToken));

        if (taxOk && kycId is not null)
        {
            var group = await TryAsync("create-account-group", () => _upvest.CreateAccountGroupAsync(user.Id, cancellationToken));
            if (group is not null)
            {
                investor.LinkAccountGroup(group.Id, group.SecuritiesAccountNumber);
                var account = await TryAsync("create-account", () => _upvest.CreateAccountAsync(user.Id, group.Id, cancellationToken));
                if (account is not null)
                {
                    investor.LinkAccount(account.Id);
                }
            }
        }

        await RefreshInvestorStatusAsync(investor, cancellationToken);
        await _investors.AddAsync(investor, cancellationToken);
        return investor;
    }

    public async Task<Investor?> GetInvestorAsync(string shopperId, CancellationToken cancellationToken = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), cancellationToken);
        if (investor is null)
        {
            return null;
        }

        await RefreshInvestorStatusAsync(investor, cancellationToken);
        await _investors.UpdateAsync(investor, cancellationToken);
        return investor;
    }

    public async Task<ModelPortfolio> UpsertModelAsync(string modelId, string name, IReadOnlyList<ModelFundInput> funds, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        var fundEntities = (funds ?? Array.Empty<ModelFundInput>())
            .Select(f => new ModelPortfolioFund(f.InstrumentId, f.Weight))
            .ToList();

        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), cancellationToken);
        bool isNew = model is null;
        if (model is null)
        {
            model = new ModelPortfolio(modelId, name, fundEntities);
        }
        else
        {
            model.ReplaceFunds(name, fundEntities);
        }

        // Mirror to Upvest as a (new) allocation. Best-effort: the model is defined in the shop
        // regardless, and the allocation id links it to Upvest for configurations and realignments.
        var entries = fundEntities.Select(f => new UpvestAllocationEntry(f.InstrumentId, f.Weight)).ToList();
        var allocationId = await TryAsync("create-allocation", () => _upvest.CreateAllocationAsync(name, entries, cancellationToken));
        if (!string.IsNullOrEmpty(allocationId))
        {
            model.SetAllocationId(allocationId);
        }

        if (isNew)
        {
            await _models.AddAsync(model, cancellationToken);
        }
        else
        {
            await _models.UpdateAsync(model, cancellationToken);
        }

        return model;
    }

    public async Task<IReadOnlyList<ModelPortfolio>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        return await _models.ListAsync(new ModelPortfoliosWithFundsSpecification(), cancellationToken);
    }

    public async Task<Investor> ChooseModelAsync(string shopperId, string modelId, CancellationToken cancellationToken = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), cancellationToken)
            ?? throw new InvestorNotFoundException();
        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), cancellationToken)
            ?? throw new ModelPortfolioNotFoundException(modelId);

        investor.FollowModel(model.ModelId, model.UpvestAllocationId);

        // Configure the investor's account to follow the model's allocation, where both exist.
        if (!string.IsNullOrEmpty(investor.UpvestAccountId) && !string.IsNullOrEmpty(model.UpvestAllocationId))
        {
            await TryAsync("create-configuration", () =>
                _upvest.CreateConfigurationAsync(investor.UpvestAccountId!, model.UpvestAllocationId!, cancellationToken));
        }

        await _investors.UpdateAsync(investor, cancellationToken);
        return investor;
    }

    public async Task<PortfolioPayment> PayInAsync(string shopperId, decimal amount, CancellationToken cancellationToken = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), cancellationToken)
            ?? throw new InvestorNotFoundException();

        await RefreshInvestorStatusAsync(investor, cancellationToken);
        await _investors.UpdateAsync(investor, cancellationToken);

        if (investor.Status != InvestorStatus.Active)
        {
            throw new InvestorNotReadyException("Upvest has not yet accepted this shopper as an investor with a ready account.");
        }
        if (string.IsNullOrEmpty(investor.FollowedModelId))
        {
            throw new InvestorNotReadyException("The investor has not chosen a model portfolio to follow.");
        }

        var payment = new PortfolioPayment(shopperId, amount, EuroCurrency);
        await _payments.AddAsync(payment, cancellationToken);

        try
        {
            var bankTxId = await _upvest.CreateBankTransactionAsync(
                new UpvestBankTransaction(amount, EuroCurrency, investor.SecuritiesAccountNumber ?? string.Empty, investor.FullName),
                cancellationToken);
            payment.RecordBankTransaction(bankTxId);

            var order = await _upvest.PlaceOrderAsync(investor.UpvestUserId!, investor.UpvestAccountId!, amount, EuroCurrency, cancellationToken);
            payment.RecordOrder(order.Id);
            payment.SetStatus(MapOrderStatus(order.Status));
        }
        catch (UpvestApiException ex)
        {
            _logger.LogWarning($"Pay-in could not be completed at Upvest ({ex.Operation}, status {ex.StatusCode}).");
            payment.SetStatus(PaymentStatus.Failed);
        }

        await _payments.UpdateAsync(payment, cancellationToken);
        return payment;
    }

    public async Task<IReadOnlyList<PortfolioPayment>> ListPaymentsAsync(string shopperId, CancellationToken cancellationToken = default)
    {
        var payments = await _payments.ListAsync(new PaymentsByShopperSpecification(shopperId), cancellationToken);
        foreach (var payment in payments.Where(p => p.Status == PaymentStatus.Pending && !string.IsNullOrEmpty(p.UpvestOrderId)))
        {
            var order = await TryAsync("get-order", () => _upvest.GetOrderAsync(payment.UpvestOrderId!, cancellationToken));
            if (order is not null)
            {
                payment.SetStatus(MapOrderStatus(order.Status));
                await _payments.UpdateAsync(payment, cancellationToken);
            }
        }
        return payments;
    }

    public async Task<Realignment> TriggerRealignmentAsync(string modelId, CancellationToken cancellationToken = default)
    {
        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), cancellationToken)
            ?? throw new ModelPortfolioNotFoundException(modelId);

        // Ensure the model has a current allocation to realign everyone to.
        if (string.IsNullOrEmpty(model.UpvestAllocationId))
        {
            var entries = model.Funds.Select(f => new UpvestAllocationEntry(f.InstrumentId, f.Weight)).ToList();
            var allocationId = await TryAsync("create-allocation", () => _upvest.CreateAllocationAsync(model.Name, entries, cancellationToken));
            if (!string.IsNullOrEmpty(allocationId))
            {
                model.SetAllocationId(allocationId);
                await _models.UpdateAsync(model, cancellationToken);
            }
        }

        // Bring each follower's configuration onto the model's current allocation before realigning,
        // so a model whose weights changed takes effect now.
        if (!string.IsNullOrEmpty(model.UpvestAllocationId))
        {
            var followers = await _investors.ListAsync(new InvestorsByModelSpecification(modelId), cancellationToken);
            foreach (var follower in followers.Where(f => !string.IsNullOrEmpty(f.UpvestAccountId) && f.FollowedAllocationId != model.UpvestAllocationId))
            {
                if (await TryAsync("create-configuration", () =>
                        _upvest.CreateConfigurationAsync(follower.UpvestAccountId!, model.UpvestAllocationId!, cancellationToken)))
                {
                    follower.FollowModel(modelId, model.UpvestAllocationId);
                    await _investors.UpdateAsync(follower, cancellationToken);
                }
            }
        }

        var realignment = new Realignment(modelId);
        await _realignments.AddAsync(realignment, cancellationToken);

        if (string.IsNullOrEmpty(model.UpvestAllocationId))
        {
            _logger.LogWarning($"Realignment for model '{modelId}' could not be triggered: no Upvest allocation is provisioned.");
            realignment.SetStatus(RealignmentStatus.Failed);
        }
        else
        {
            try
            {
                var executionId = await _upvest.TriggerRebalancingByAllocationAsync(model.UpvestAllocationId!, cancellationToken);
                realignment.RecordExecution(executionId);
                await RefreshRealignmentStatusAsync(realignment, cancellationToken);
            }
            catch (UpvestApiException ex)
            {
                _logger.LogWarning($"Realignment for model '{modelId}' could not be triggered at Upvest ({ex.Operation}, status {ex.StatusCode}).");
                realignment.SetStatus(RealignmentStatus.Failed);
            }
        }

        await _realignments.UpdateAsync(realignment, cancellationToken);
        return realignment;
    }

    public async Task<Realignment?> GetRealignmentAsync(string realignmentId, CancellationToken cancellationToken = default)
    {
        var realignment = await _realignments.FirstOrDefaultAsync(new RealignmentByPublicIdSpecification(realignmentId), cancellationToken);
        if (realignment is null)
        {
            return null;
        }

        if (realignment.Status == RealignmentStatus.Pending && !string.IsNullOrEmpty(realignment.UpvestExecutionId))
        {
            await RefreshRealignmentStatusAsync(realignment, cancellationToken);
            await _realignments.UpdateAsync(realignment, cancellationToken);
        }
        return realignment;
    }

    public async Task<PortfolioView?> GetPortfolioAsync(string shopperId, CancellationToken cancellationToken = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), cancellationToken);
        if (investor is null)
        {
            return null;
        }

        var payments = await _payments.ListAsync(new PaymentsByShopperSpecification(shopperId), cancellationToken);
        var paidIn = payments.Where(p => p.Status != PaymentStatus.Failed).Sum(p => p.Amount);

        var holdings = new List<PortfolioHoldingView>();
        decimal totalValue = 0m;

        if (!string.IsNullOrEmpty(investor.UpvestAccountId))
        {
            var valuation = await TryAsync("get-valuation", () => _upvest.GetAccountValuationAsync(investor.UpvestAccountId!, cancellationToken));
            if (valuation is not null)
            {
                holdings = valuation.Positions
                    .Select(p => new PortfolioHoldingView(p.InstrumentId, p.Quantity, p.Value ?? 0m))
                    .ToList();
                totalValue = valuation.TotalValue;
            }
        }

        return new PortfolioView(investor.FollowedModelId, holdings, totalValue, paidIn);
    }

    // ----- helpers -------------------------------------------------------------------------

    private async Task RefreshInvestorStatusAsync(Investor investor, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(investor.UpvestUserId))
        {
            investor.SetStatus(InvestorStatus.Pending);
            return;
        }

        var user = await TryAsync("get-user", () => _upvest.GetUserAsync(investor.UpvestUserId!, cancellationToken));
        if (user is not null && IsRejectedUserStatus(user.Status))
        {
            investor.SetStatus(InvestorStatus.Rejected);
            return;
        }

        if (!string.IsNullOrEmpty(investor.UpvestAccountId))
        {
            var account = await TryAsync("get-account", () => _upvest.GetAccountAsync(investor.UpvestAccountId!, cancellationToken));
            if (account is not null)
            {
                investor.SetStatus(MapAccountStatus(account.Status));
                return;
            }
        }

        investor.SetStatus(InvestorStatus.Pending);
    }

    private async Task RefreshRealignmentStatusAsync(Realignment realignment, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(realignment.UpvestExecutionId))
        {
            return;
        }

        var orders = await TryAsync("get-rebalancing-orders", () => _upvest.GetRebalancingOrdersAsync(realignment.UpvestExecutionId!, cancellationToken));
        if (orders is null)
        {
            return;
        }

        realignment.SetStatus(MapRebalancingStatus(orders));
    }

    // A freshly created user is INACTIVE until onboarding (KYC) completes — that is 'pending', not
    // rejected. Only an offboarded user means Upvest will not take the shopper on.
    private static bool IsRejectedUserStatus(string status) =>
        status is "OFFBOARDING" or "OFFBOARDED";

    private static InvestorStatus MapAccountStatus(string status) => status switch
    {
        "ACTIVE" => InvestorStatus.Active,
        "CLOSING" or "CLOSED" or "LOCKED" => InvestorStatus.Rejected,
        _ => InvestorStatus.Pending
    };

    private static PaymentStatus MapOrderStatus(string status) => status switch
    {
        "FILLED" or "SETTLED" => PaymentStatus.Invested,
        "CANCELLED" => PaymentStatus.Failed,
        _ => PaymentStatus.Pending
    };

    private static RealignmentStatus MapRebalancingStatus(IReadOnlyList<UpvestRebalancingOrder> orders)
    {
        if (orders.Count == 0)
        {
            return RealignmentStatus.Pending;
        }
        if (orders.Any(o => o.Status == "CANCELLED"))
        {
            return RealignmentStatus.Failed;
        }
        if (orders.All(o => o.Status is "FILLED" or "SETTLED"))
        {
            return RealignmentStatus.Completed;
        }
        return RealignmentStatus.Pending;
    }

    /// <summary>Runs an Upvest call, swallowing <see cref="UpvestApiException"/> and logging it (no PII).</summary>
    private async Task<T?> TryAsync<T>(string operation, Func<Task<T?>> action) where T : class
    {
        try
        {
            return await action();
        }
        catch (UpvestApiException ex)
        {
            _logger.LogWarning($"Upvest call '{operation}' not completed (status {ex.StatusCode}).");
            return null;
        }
    }

    /// <summary>Overload for Upvest calls that return no value.</summary>
    private async Task<bool> TryAsync(string operation, Func<Task> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (UpvestApiException ex)
        {
            _logger.LogWarning($"Upvest call '{operation}' not completed (status {ex.StatusCode}).");
            return false;
        }
    }
}
