using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Default <see cref="IPortfolioService"/>. Keeps the shop's local records (investor links,
/// model definitions, payments, realignments) and drives Upvest for every externally-observable
/// fact. Statuses are always read back from Upvest so the shop reports Upvest's truth.
/// </summary>
public class PortfolioService : IPortfolioService
{
    private const string Euro = "EUR";

    private readonly IRepository<Investor> _investors;
    private readonly IRepository<ModelPortfolio> _models;
    private readonly IRepository<PortfolioPayment> _payments;
    private readonly IRepository<Realignment> _realignments;
    private readonly IUpvestClient _upvest;
    private readonly IAppLogger<PortfolioService> _logger;

    public PortfolioService(
        IRepository<Investor> investors,
        IRepository<ModelPortfolio> models,
        IRepository<PortfolioPayment> payments,
        IRepository<Realignment> realignments,
        IUpvestClient upvest,
        IAppLogger<PortfolioService> logger)
    {
        _investors = investors;
        _models = models;
        _payments = payments;
        _realignments = realignments;
        _upvest = upvest;
        _logger = logger;
    }

    // ---------------------------------------------------------------- Flow 1

    public async Task<InvestorView> RegisterInvestorAsync(string buyerId, InvestorRegistration registration, CancellationToken ct)
    {
        var existing = await _investors.FirstOrDefaultAsync(new InvestorByBuyerSpecification(buyerId), ct);
        if (existing is not null)
        {
            throw PortfolioException.Conflict("This shopper is already signed up as an investor.");
        }

        // Create the user at Upvest. This is the point at which the shopper becomes an investor.
        var user = await _upvest.CreateUserAsync(registration, ct);

        var investor = new Investor(buyerId, user.Id);
        await _investors.AddAsync(investor, ct);

        // Continue onboarding so the investor's account can become ready. Each of these steps
        // needs a scope beyond user operations; where the environment withholds it, the investor
        // simply stays pending. None of this ever blocks the sign-up itself.
        await TryContinueOnboardingAsync(investor, registration, ct);

        var status = await ResolveInvestorStatusAsync(investor, user, ct);
        return new InvestorView(investor.UpvestUserId, status);
    }

    private async Task TryContinueOnboardingAsync(Investor investor, InvestorRegistration registration, CancellationToken ct)
    {
        await TryStepAsync("set-tax-residency", () =>
            _upvest.SetTaxResidencyAsync(investor.UpvestUserId, registration.TaxCountry, registration.TaxId, ct));

        await TryStepAsync("submit-identity-check", () =>
            _upvest.SubmitIdentityCheckAsync(investor.UpvestUserId, registration, ct));

        var accountGroupId = await TryStepAsync("create-account-group", () =>
            _upvest.CreateAccountGroupAsync(investor.UpvestUserId, ct));
        if (!string.IsNullOrEmpty(accountGroupId))
        {
            investor.SetAccountGroup(accountGroupId);
            var account = await TryStepAsync("create-account", () =>
                _upvest.CreateAccountAsync(investor.UpvestUserId, accountGroupId, ct));
            if (account is not null && !string.IsNullOrEmpty(account.Id))
            {
                investor.SetAccount(account.Id);
            }
            await _investors.UpdateAsync(investor, ct);
        }
    }

    public async Task<InvestorView?> GetInvestorAsync(string buyerId, CancellationToken ct)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerSpecification(buyerId), ct);
        if (investor is null)
        {
            return null;
        }

        var user = await _upvest.GetUserAsync(investor.UpvestUserId, ct);
        var status = await ResolveInvestorStatusAsync(investor, user, ct);
        return new InvestorView(investor.UpvestUserId, status);
    }

    /// <summary>
    /// Maps Upvest's user (and account) state onto the sign-up status. An investor is only
    /// <c>active</c> once Upvest has accepted the user AND their portfolio account is ready.
    /// </summary>
    private async Task<string> ResolveInvestorStatusAsync(Investor investor, UpvestUser user, CancellationToken ct)
    {
        switch (user.Status)
        {
            case "OFFBOARDING":
            case "OFFBOARDED":
                return PortfolioStatuses.Rejected;
            case "ACTIVE":
                break;
            default: // INACTIVE and any not-yet-active state.
                return PortfolioStatuses.Pending;
        }

        // User is accepted; the account must also be ready to hold the portfolio.
        if (investor.UpvestAccountId is null)
        {
            return PortfolioStatuses.Pending;
        }

        try
        {
            var account = await _upvest.GetAccountAsync(investor.UpvestAccountId, ct);
            return account.Status == "ACTIVE" ? PortfolioStatuses.Active : PortfolioStatuses.Pending;
        }
        catch (UpvestApiException ex)
        {
            _logger.LogWarning($"Could not read Upvest account status for investor; reporting pending. {Describe(ex)}");
            return PortfolioStatuses.Pending;
        }
    }

    // ---------------------------------------------------------------- Flow 2

    public async Task<ModelView> UpsertModelAsync(string modelId, string name, IReadOnlyList<FundWeight> funds, CancellationToken ct)
    {
        var fundEntities = funds.Select(f => new ModelFund(f.InstrumentId, f.WeightPercent)).ToList();

        var model = await _models.FirstOrDefaultAsync(new ModelByModelIdSpecification(modelId), ct);
        bool isNew = model is null;
        if (model is null)
        {
            model = new ModelPortfolio(modelId, name, fundEntities);
        }
        else
        {
            model.Replace(name, fundEntities);
        }

        // Mirror the model to an Upvest allocation. If the environment withholds the portfolios
        // scope the model is still defined locally and will sync once the scope is available.
        try
        {
            if (model.UpvestAllocationId is null)
            {
                var allocationId = await _upvest.CreateAllocationAsync(name, funds, ct);
                model.SetAllocationId(allocationId);
            }
            else
            {
                await _upvest.ReplaceAllocationAsync(model.UpvestAllocationId, name, funds, ct);
            }
        }
        catch (UpvestApiException ex)
        {
            _logger.LogWarning($"Model '{modelId}' saved locally but not yet mirrored to an Upvest allocation. {Describe(ex)}");
        }

        if (isNew)
        {
            await _models.AddAsync(model, ct);
        }
        else
        {
            await _models.UpdateAsync(model, ct);
        }

        return ToView(model);
    }

    public async Task<IReadOnlyList<ModelView>> ListModelsAsync(CancellationToken ct)
    {
        var models = await _models.ListAsync(new AllModelsSpecification(), ct);
        return models.Select(ToView).ToList();
    }

    public async Task SelectModelAsync(string buyerId, string modelId, CancellationToken ct)
    {
        var investor = await RequireInvestorAsync(buyerId, ct);
        var model = await _models.FirstOrDefaultAsync(new ModelByModelIdSpecification(modelId), ct)
            ?? throw PortfolioException.NotFound($"No model portfolio with id '{modelId}'.");

        investor.SelectModel(modelId);
        await _investors.UpdateAsync(investor, ct);

        // Link the investor's account to the model's allocation at Upvest, when both exist.
        if (investor.UpvestAccountId is not null && model.UpvestAllocationId is not null)
        {
            try
            {
                await _upvest.SetAccountAllocationAsync(investor.UpvestAccountId, model.UpvestAllocationId, ct);
            }
            catch (UpvestApiException ex)
            {
                _logger.LogWarning($"Recorded model choice locally; Upvest portfolio configuration not updated. {Describe(ex)}");
            }
        }
    }

    // ---------------------------------------------------------------- Flow 3

    public async Task<PaymentView> PayInAsync(string buyerId, decimal amount, CancellationToken ct)
    {
        if (amount <= 0m)
        {
            throw PortfolioException.BadRequest("The amount paid in must be greater than zero.");
        }

        var investor = await RequireInvestorAsync(buyerId, ct);

        // A payment is only accepted once Upvest has accepted the investor and their account is
        // ready, and a model has been chosen.
        var user = await _upvest.GetUserAsync(investor.UpvestUserId, ct);
        var status = await ResolveInvestorStatusAsync(investor, user, ct);
        if (status != PortfolioStatuses.Active)
        {
            throw PortfolioException.Conflict(
                "Paying in is only possible once Upvest has accepted you as an investor and your account is ready.");
        }
        if (investor.SelectedModelId is null)
        {
            throw PortfolioException.Conflict("Choose a model portfolio before paying in.");
        }

        var payment = new PortfolioPayment(buyerId, amount, Euro);
        await _payments.AddAsync(payment, ct);

        // Credit the cash to the investor's account, then invest it across the model's funds.
        var topupId = await _upvest.CreateTopupAsync(investor.UpvestAccountGroupId!, amount, Euro, ct);
        payment.SetTopup(topupId);

        var order = await _upvest.CreatePortfolioBuyOrderAsync(investor.UpvestUserId, investor.UpvestAccountId!, amount, Euro, ct);
        payment.SetOrder(order.Id);
        payment.UpdateStatus(MapOrderStatus(order.Status));

        await _payments.UpdateAsync(payment, ct);
        return new PaymentView(payment.PublicId, payment.Amount, payment.Status);
    }

    public async Task<IReadOnlyList<PaymentView>> ListPaymentsAsync(string buyerId, CancellationToken ct)
    {
        var payments = await _payments.ListAsync(new PaymentsByBuyerSpecification(buyerId), ct);

        var views = new List<PaymentView>(payments.Count);
        foreach (var payment in payments)
        {
            if (payment.UpvestOrderId is not null && !IsTerminalPaymentStatus(payment.Status))
            {
                try
                {
                    var order = await _upvest.GetPortfolioOrderAsync(payment.UpvestOrderId, ct);
                    var mapped = MapOrderStatus(order.Status);
                    if (mapped != payment.Status)
                    {
                        payment.UpdateStatus(mapped);
                        await _payments.UpdateAsync(payment, ct);
                    }
                }
                catch (UpvestApiException ex)
                {
                    _logger.LogWarning($"Could not refresh payment status from Upvest; reporting last known. {Describe(ex)}");
                }
            }

            views.Add(new PaymentView(payment.PublicId, payment.Amount, payment.Status));
        }

        return views;
    }

    // ---------------------------------------------------------------- Flow 4

    public async Task<RealignmentView> TriggerRealignmentAsync(string modelId, CancellationToken ct)
    {
        var model = await _models.FirstOrDefaultAsync(new ModelByModelIdSpecification(modelId), ct)
            ?? throw PortfolioException.NotFound($"No model portfolio with id '{modelId}'.");

        if (model.UpvestAllocationId is null)
        {
            throw PortfolioException.Conflict(
                $"Model '{modelId}' has not been mirrored to an Upvest allocation yet, so its portfolios cannot be realigned.");
        }

        var realignment = new Realignment(modelId);
        await _realignments.AddAsync(realignment, ct);

        var executionId = await _upvest.TriggerRealignmentAsync(model.UpvestAllocationId, ct);
        realignment.SetExecution(executionId);

        var execution = await _upvest.GetRealignmentAsync(executionId, ct);
        realignment.UpdateStatus(MapRealignmentStatus(execution.Status));
        await _realignments.UpdateAsync(realignment, ct);

        return new RealignmentView(realignment.PublicId, realignment.Status);
    }

    public async Task<RealignmentView> GetRealignmentAsync(Guid realignmentId, CancellationToken ct)
    {
        var realignment = await _realignments.FirstOrDefaultAsync(new RealignmentByPublicIdSpecification(realignmentId), ct)
            ?? throw PortfolioException.NotFound($"No realignment with id '{realignmentId}'.");

        if (realignment.UpvestExecutionId is not null && realignment.Status == PortfolioStatuses.Pending)
        {
            try
            {
                var execution = await _upvest.GetRealignmentAsync(realignment.UpvestExecutionId, ct);
                var mapped = MapRealignmentStatus(execution.Status);
                if (mapped != realignment.Status)
                {
                    realignment.UpdateStatus(mapped);
                    await _realignments.UpdateAsync(realignment, ct);
                }
            }
            catch (UpvestApiException ex)
            {
                _logger.LogWarning($"Could not refresh realignment status from Upvest; reporting last known. {Describe(ex)}");
            }
        }

        return new RealignmentView(realignment.PublicId, realignment.Status);
    }

    // ---------------------------------------------------------------- Flow 5

    public async Task<PortfolioView> GetPortfolioAsync(string buyerId, CancellationToken ct)
    {
        var investor = await RequireInvestorAsync(buyerId, ct);

        var payments = await _payments.ListAsync(new PaymentsByBuyerSpecification(buyerId), ct);
        var paidIn = payments
            .Where(p => p.Status != PortfolioStatuses.Failed)
            .Sum(p => p.Amount);

        IReadOnlyList<HoldingView> holdings = Array.Empty<HoldingView>();
        decimal totalValue = 0m;

        if (investor.UpvestAccountId is not null)
        {
            var valuation = await _upvest.GetValuationAsync(investor.UpvestAccountId, ct);
            holdings = valuation.Holdings
                .Select(h => new HoldingView(h.InstrumentId, h.Quantity, h.Value))
                .ToList();
            totalValue = valuation.TotalValue;
        }

        return new PortfolioView(investor.SelectedModelId, holdings, totalValue, paidIn);
    }

    // ---------------------------------------------------------------- Helpers

    private async Task<Investor> RequireInvestorAsync(string buyerId, CancellationToken ct)
    {
        return await _investors.FirstOrDefaultAsync(new InvestorByBuyerSpecification(buyerId), ct)
            ?? throw PortfolioException.NotFound("This shopper is not signed up as an investor.");
    }

    private static ModelView ToView(ModelPortfolio model) => new(
        model.ModelId,
        model.Name,
        model.Funds.Select(f => new ModelFundView(f.InstrumentId, f.Weight)).ToList());

    private static string MapOrderStatus(string upvestStatus) => upvestStatus switch
    {
        "FILLED" or "SETTLED" => PortfolioStatuses.Invested,
        "CANCELLED" => PortfolioStatuses.Failed,
        _ => PortfolioStatuses.Pending, // NEW, PROCESSING
    };

    private static string MapRealignmentStatus(string upvestStatus) => upvestStatus switch
    {
        "COMPLETED" or "FILLED" or "SETTLED" => PortfolioStatuses.Completed,
        "CANCELLED" or "FAILED" => PortfolioStatuses.Failed,
        _ => PortfolioStatuses.Pending,
    };

    private static bool IsTerminalPaymentStatus(string status) =>
        status == PortfolioStatuses.Invested || status == PortfolioStatuses.Failed;

    /// <summary>A log-safe description of an Upvest failure (status + Upvest message, no personal data).</summary>
    private static string Describe(UpvestApiException ex) => $"[{ex.Operation}] HTTP {ex.StatusCode}: {ex.Detail}";

    private async Task TryStepAsync(string step, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (UpvestApiException ex)
        {
            _logger.LogWarning($"Onboarding step '{step}' not completed. {Describe(ex)}");
        }
    }

    private async Task<T?> TryStepAsync<T>(string step, Func<Task<T>> action) where T : class
    {
        try
        {
            return await action();
        }
        catch (UpvestApiException ex)
        {
            _logger.LogWarning($"Onboarding step '{step}' not completed. {Describe(ex)}");
            return null;
        }
    }
}
