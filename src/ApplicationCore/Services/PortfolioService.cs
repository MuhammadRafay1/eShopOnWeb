using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Result;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class PortfolioService : IPortfolioService
{
    private const string Eur = "EUR";

    private readonly IUpvestGateway _upvest;
    private readonly IRepository<Investor> _investors;
    private readonly IRepository<ModelPortfolio> _models;
    private readonly IRepository<PortfolioPayment> _payments;
    private readonly IRepository<PortfolioRealignment> _realignments;
    private readonly IAppLogger<PortfolioService> _logger;

    public PortfolioService(
        IUpvestGateway upvest,
        IRepository<Investor> investors,
        IRepository<ModelPortfolio> models,
        IRepository<PortfolioPayment> payments,
        IRepository<PortfolioRealignment> realignments,
        IAppLogger<PortfolioService> logger)
    {
        _upvest = upvest;
        _investors = investors;
        _models = models;
        _payments = payments;
        _realignments = realignments;
        _logger = logger;
    }

    // ----------------------------------------------------------------- Flow 1: Investor

    public async Task<Result<InvestorView>> RegisterInvestorAsync(string buyerId, InvestorSignUp signUp, CancellationToken ct = default)
    {
        var validationErrors = ValidateSignUp(signUp);
        if (validationErrors.Count > 0)
        {
            return Result<InvestorView>.Invalid(validationErrors);
        }

        var existing = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), ct);
        if (existing != null)
        {
            // Idempotent: a shopper who is already an investor just gets their current status back.
            return await BuildInvestorViewAsync(existing, ct);
        }

        // Create the Upvest user first (this is the only step the sandbox client is scoped for).
        var user = await _upvest.CreateUserAsync(signUp, ct);

        var investor = new Investor(buyerId, user.Id);
        await _investors.AddAsync(investor, ct);

        // Reporting / regulatory steps — best effort: a failure here (e.g. a missing scope in this
        // environment) must not lose the investor record we just created.
        await TryStepAsync("tax_residencies", () => _upvest.SetTaxResidenciesAsync(user.Id, signUp.TaxCountry, signUp.TaxId, ct));
        await TryStepAsync("user_identifier", () => _upvest.CreateIdentifierAsync(user.Id, signUp.IdentityDocument, ct));
        await TryStepAsync("kyc_check", () => _upvest.CreateKycCheckAsync(user.Id, signUp.IdentityDocument, signUp.Nationality, ct));

        // Provision the account group + managed-portfolio account the investor will hold.
        await TryStepAsync("account_group", async () =>
        {
            var group = await _upvest.CreateAccountGroupAsync(user.Id, ct);
            investor.SetAccountGroup(group.Id);
            await TryStepAsync("account", async () =>
            {
                var account = await _upvest.CreateAccountAsync(user.Id, group.Id, ct);
                investor.SetAccount(account.Id);
            });
        });

        await _investors.UpdateAsync(investor, ct);
        return await BuildInvestorViewAsync(investor, ct);
    }

    public async Task<Result<InvestorView>> GetInvestorAsync(string buyerId, CancellationToken ct = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), ct);
        if (investor == null)
        {
            return Result<InvestorView>.NotFound("This shopper is not registered as an investor.");
        }

        return await BuildInvestorViewAsync(investor, ct);
    }

    private async Task<Result<InvestorView>> BuildInvestorViewAsync(Investor investor, CancellationToken ct)
    {
        var user = await _upvest.GetUserAsync(investor.UpvestUserId, ct);

        string? accountStatus = null;
        if (investor.UpvestAccountId is Guid accountId)
        {
            try
            {
                var account = await _upvest.GetAccountAsync(accountId, ct);
                accountStatus = account.Status;
            }
            catch (UpvestApiException ex)
            {
                _logger.LogWarning("Could not read account status for an investor ({Status}).", ex.StatusCode);
            }
        }

        var status = PortfolioStatus.FromUpvest(user.Status, accountStatus);
        return Result<InvestorView>.Success(new InvestorView(investor.UpvestUserId, status));
    }

    // ----------------------------------------------------------------- Flow 2: Models

    public async Task<Result<ModelView>> UpsertModelAsync(string modelId, string name, IEnumerable<FundView> funds, CancellationToken ct = default)
    {
        var fundList = funds?.ToList() ?? new List<FundView>();

        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), ct);
        try
        {
            var modelFunds = fundList.Select(f => new ModelFund(f.InstrumentId, f.Weight)).ToList();
            if (model == null)
            {
                model = new ModelPortfolio(modelId, name, modelFunds);
                await _models.AddAsync(model, ct);
            }
            else
            {
                model.Update(name, modelFunds);
            }
        }
        catch (ArgumentException ex)
        {
            return Result<ModelView>.Invalid(new List<ValidationError> { new() { ErrorMessage = ex.Message } });
        }

        // Mirror the model as a portfolio allocation at Upvest (best effort w.r.t. environment scopes).
        var allocationFunds = model.Funds.Select(f => new AllocationFund(f.InstrumentId, f.Weight));
        await TryStepAsync("allocation", async () =>
        {
            if (model.UpvestAllocationId is Guid allocId)
            {
                await _upvest.UpdateAllocationAsync(allocId, model.Name, allocationFunds, ct);
            }
            else
            {
                var created = await _upvest.CreateAllocationAsync(model.Name, allocationFunds, ct);
                model.LinkAllocation(created.Id);
            }
        });

        await _models.UpdateAsync(model, ct);
        return Result<ModelView>.Success(ToView(model));
    }

    public async Task<Result<IReadOnlyList<ModelView>>> GetModelsAsync(CancellationToken ct = default)
    {
        var models = await _models.ListAsync(new ModelPortfoliosWithFundsSpecification(), ct);
        IReadOnlyList<ModelView> views = models.Select(ToView).ToList();
        return Result<IReadOnlyList<ModelView>>.Success(views);
    }

    public async Task<Result> SelectModelAsync(string buyerId, string modelId, CancellationToken ct = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), ct);
        if (investor == null)
        {
            return Result.NotFound("This shopper is not registered as an investor.");
        }

        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), ct);
        if (model == null)
        {
            return Result.NotFound($"No model portfolio '{modelId}' exists.");
        }

        var previouslyChosen = investor.SelectedModelId != null;
        investor.SelectModel(modelId);
        await _investors.UpdateAsync(investor, ct);

        // Point the investor's portfolio configuration at the chosen allocation (best effort).
        if (investor.UpvestAccountId is Guid accountId && model.UpvestAllocationId is Guid allocationId)
        {
            await TryStepAsync("configuration", () => previouslyChosen
                ? _upvest.UpdateConfigurationAsync(accountId, allocationId, ct)
                : _upvest.CreateConfigurationAsync(accountId, allocationId, ct));
        }

        return Result.Success();
    }

    // ----------------------------------------------------------------- Flow 3: Payments

    public async Task<Result<PaymentView>> PayAsync(string buyerId, decimal amount, CancellationToken ct = default)
    {
        if (amount <= 0)
        {
            return Result<PaymentView>.Invalid(new List<ValidationError> { new() { ErrorMessage = "Amount must be greater than zero." } });
        }

        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), ct);
        if (investor == null)
        {
            return Result<PaymentView>.NotFound("This shopper is not registered as an investor.");
        }

        if (investor.SelectedModelId == null)
        {
            return Result<PaymentView>.Invalid(new List<ValidationError> { new() { ErrorMessage = "Choose a model portfolio before paying in." } });
        }

        // The payment is only allowed once Upvest has accepted the investor and their account is ready.
        var statusView = await BuildInvestorViewAsync(investor, ct);
        if (statusView.Value.Status != PortfolioStatus.Active)
        {
            return Result<PaymentView>.Invalid(new List<ValidationError>
            {
                new() { ErrorMessage = "Your investor account is not yet active; a pay-in cannot be accepted." }
            });
        }

        var payment = new PortfolioPayment(investor.Id, amount, Eur);
        await _payments.AddAsync(payment, ct);

        // Credit the cash, then invest it across the model's funds.
        if (investor.UpvestAccountGroupId is Guid groupId)
        {
            var increase = await _upvest.IncreaseVirtualCashAsync(groupId, amount, Eur, ct);
            payment.SetVirtualCashIncrease(increase.Id);
        }

        var order = await _upvest.CreatePortfolioOrderAsync(investor.UpvestUserId, investor.UpvestAccountId!.Value, amount, Eur, "BUY", ct);
        payment.SetPortfolioOrder(order.Id);
        await _payments.UpdateAsync(payment, ct);

        return Result<PaymentView>.Success(new PaymentView(payment.PublicId, payment.Amount, PortfolioStatus.FromPortfolioOrder(order.Status)));
    }

    public async Task<Result<IReadOnlyList<PaymentView>>> GetPaymentsAsync(string buyerId, CancellationToken ct = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), ct);
        if (investor == null)
        {
            return Result<IReadOnlyList<PaymentView>>.NotFound("This shopper is not registered as an investor.");
        }

        var payments = await _payments.ListAsync(new PaymentsByInvestorSpecification(investor.Id), ct);
        var views = new List<PaymentView>(payments.Count);
        foreach (var payment in payments)
        {
            views.Add(new PaymentView(payment.PublicId, payment.Amount, await ResolvePaymentStatusAsync(payment, ct)));
        }

        return Result<IReadOnlyList<PaymentView>>.Success(views);
    }

    private async Task<string> ResolvePaymentStatusAsync(PortfolioPayment payment, CancellationToken ct)
    {
        if (payment.UpvestPortfolioOrderId is not Guid orderId)
        {
            return PortfolioStatus.Pending;
        }

        try
        {
            var order = await _upvest.GetPortfolioOrderAsync(orderId, ct);
            return PortfolioStatus.FromPortfolioOrder(order.Status);
        }
        catch (UpvestApiException ex)
        {
            _logger.LogWarning("Could not read portfolio-order status ({Status}).", ex.StatusCode);
            return PortfolioStatus.Pending;
        }
    }

    // ----------------------------------------------------------------- Flow 4: Realignment

    public async Task<Result<RealignmentView>> TriggerRealignmentAsync(string modelId, CancellationToken ct = default)
    {
        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), ct);
        if (model == null)
        {
            return Result<RealignmentView>.NotFound($"No model portfolio '{modelId}' exists.");
        }

        if (model.UpvestAllocationId is not Guid allocationId)
        {
            return Result<RealignmentView>.Invalid(new List<ValidationError>
            {
                new() { ErrorMessage = "This model is not yet provisioned at Upvest, so it cannot be realigned." }
            });
        }

        var executionId = await _upvest.TriggerRebalancingAsync(allocationId, ct);
        var realignment = new PortfolioRealignment(modelId, executionId);
        await _realignments.AddAsync(realignment, ct);

        return Result<RealignmentView>.Success(new RealignmentView(realignment.PublicId, PortfolioStatus.Pending));
    }

    public async Task<Result<RealignmentView>> GetRealignmentAsync(Guid realignmentId, CancellationToken ct = default)
    {
        var realignment = await _realignments.FirstOrDefaultAsync(new RealignmentByPublicIdSpecification(realignmentId), ct);
        if (realignment == null)
        {
            return Result<RealignmentView>.NotFound("No such realignment.");
        }

        var status = PortfolioStatus.Pending;
        try
        {
            var execution = await _upvest.GetRebalancingExecutionAsync(realignment.UpvestExecutionId, ct);
            status = PortfolioStatus.FromRebalancingOrders(execution.OrderStatuses);
        }
        catch (UpvestApiException ex)
        {
            _logger.LogWarning("Could not read rebalancing execution status ({Status}).", ex.StatusCode);
        }

        return Result<RealignmentView>.Success(new RealignmentView(realignment.PublicId, status));
    }

    // ----------------------------------------------------------------- Flow 5: Portfolio

    public async Task<Result<PortfolioView>> GetPortfolioAsync(string buyerId, CancellationToken ct = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), ct);
        if (investor == null)
        {
            return Result<PortfolioView>.NotFound("This shopper is not registered as an investor.");
        }

        var payments = await _payments.ListAsync(new PaymentsByInvestorSpecification(investor.Id), ct);
        var paidIn = payments.Sum(p => p.Amount);

        IReadOnlyList<HoldingView> holdings = Array.Empty<HoldingView>();
        decimal totalValue = 0m;

        if (investor.UpvestAccountId is Guid accountId)
        {
            try
            {
                var valuation = await _upvest.GetValuationAsync(accountId, ct);
                holdings = valuation.Holdings
                    .Select(h => new HoldingView(h.Isin, h.Quantity, h.Value))
                    .ToList();
                totalValue = valuation.TotalValue;
            }
            catch (UpvestApiException ex)
            {
                _logger.LogWarning("Could not read portfolio valuation ({Status}).", ex.StatusCode);
            }
        }

        return Result<PortfolioView>.Success(new PortfolioView(investor.SelectedModelId, holdings, totalValue, paidIn));
    }

    // ----------------------------------------------------------------- Helpers

    private static List<ValidationError> ValidateSignUp(InvestorSignUp s)
    {
        var errors = new List<ValidationError>();
        void Require(string? value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add(new ValidationError { Identifier = field, ErrorMessage = $"{field} is required." });
            }
        }

        Require(s.FirstName, "firstName");
        Require(s.LastName, "lastName");
        Require(s.Email, "email");
        Require(s.BirthDate, "birthDate");
        Require(s.Nationality, "nationality");
        Require(s.TaxId, "taxId");
        Require(s.TaxCountry, "taxCountry");
        Require(s.Address?.Line1, "address.line1");
        Require(s.Address?.Postcode, "address.postcode");
        Require(s.Address?.City, "address.city");
        Require(s.Address?.Country, "address.country");
        Require(s.IdentityDocument?.Type, "identityDocument.type");
        Require(s.IdentityDocument?.Number, "identityDocument.number");
        Require(s.IdentityDocument?.IssuingCountry, "identityDocument.issuingCountry");
        Require(s.IdentityDocument?.ExpiryDate, "identityDocument.expiryDate");
        return errors;
    }

    private static ModelView ToView(ModelPortfolio model) =>
        new(model.ModelId, model.Name, model.Funds.Select(f => new FundView(f.InstrumentId, f.Weight)).ToList());

    private async Task TryStepAsync(string step, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (UpvestApiException ex)
        {
            _logger.LogWarning("Upvest onboarding/provisioning step '{Step}' did not complete (status {Status}).", step, ex.StatusCode);
        }
    }
}
