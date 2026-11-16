using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class PortfolioService : IPortfolioService
{
    private const string Eur = "EUR";

    private readonly IRepository<Investor> _investors;
    private readonly IRepository<ModelPortfolio> _models;
    private readonly IRepository<Payment> _payments;
    private readonly IRepository<Realignment> _realignments;
    private readonly IUpvestInvestmentApi _upvest;
    private readonly IAppLogger<PortfolioService> _logger;

    public PortfolioService(
        IRepository<Investor> investors,
        IRepository<ModelPortfolio> models,
        IRepository<Payment> payments,
        IRepository<Realignment> realignments,
        IUpvestInvestmentApi upvest,
        IAppLogger<PortfolioService> logger)
    {
        _investors = investors;
        _models = models;
        _payments = payments;
        _realignments = realignments;
        _upvest = upvest;
        _logger = logger;
    }

    // ----------------------------------------------------------------- Flow 1

    public async Task<InvestorView> SignUpInvestorAsync(string shopperId, InvestorSignUp signUp, CancellationToken ct = default)
    {
        var existing = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), ct);
        if (existing is not null)
        {
            // A shopper signs up once; re-sync and return the current state rather than duplicating.
            await ReconcileInvestorAsync(existing, ct);
            return ToView(existing);
        }

        var investor = new Investor(shopperId);

        try
        {
            var user = await _upvest.CreateUserAsync(signUp, ct);
            investor.SetUpvestUser(user.Id);
            _logger.LogInformation("Upvest user created for investor sign-up (status {Status}).", user.Status);

            // Regulatory follow-ups. Best-effort: a sandbox scope limit must not abort sign-up.
            await BestEffortAsync("submit KYC check", () => _upvest.SubmitKycCheckAsync(user.Id, signUp, ct));
            await BestEffortAsync("set tax residency", () => _upvest.SetTaxResidencyAsync(user.Id, signUp.TaxCountry, signUp.TaxId, ct));

            // Create the account group and account that will hold the managed portfolio.
            string? accountStatus = null;
            await BestEffortAsync("create account", async () =>
            {
                var group = await _upvest.CreateAccountGroupAsync(user.Id, ct);
                var account = await _upvest.CreateAccountAsync(user.Id, group.Id, ct);
                investor.SetUpvestAccount(group.Id, account.Id);
                accountStatus = account.Status;
            });

            // Status straight from the create responses (the sandbox may not grant read scopes).
            investor.SetStatus(ComputeInvestorStatus(investor, user.Status, accountStatus));
        }
        catch (UpvestApiException ex) when (ex.StatusCode is 400 or 422)
        {
            // The submitted sign-up details were invalid; tell the shopper so they can correct them.
            _logger.LogWarning("Upvest rejected the investor sign-up details (HTTP {Status}).", ex.StatusCode);
            throw new PortfolioException(ex.Message, 400);
        }
        catch (UpvestApiException ex)
        {
            // Could not reach/complete user creation (e.g. a scope limit); keep the record pending.
            _logger.LogWarning("Upvest could not complete investor sign-up at user creation (HTTP {Status}).", ex.StatusCode);
            investor.SetStatus(InvestorStatus.Pending);
        }

        await _investors.AddAsync(investor, ct);
        return ToView(investor);
    }

    public async Task<InvestorView?> GetInvestorAsync(string shopperId, CancellationToken ct = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), ct);
        if (investor is null) return null;

        await ReconcileInvestorAsync(investor, ct);
        return ToView(investor);
    }

    // ----------------------------------------------------------------- Flow 2

    public async Task<ModelView> UpsertModelAsync(int modelId, string name, IReadOnlyList<FundWeightInput> funds, CancellationToken ct = default)
    {
        var fundEntities = funds.Select(f => new ModelFund(f.InstrumentId, f.Weight)).ToList();

        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByIdSpecification(modelId), ct);
        if (model is null)
        {
            model = new ModelPortfolio(modelId, name, fundEntities); // validates weights sum to 100
            await BestEffortAsync("create allocation", async () =>
            {
                var allocationId = await _upvest.CreateAllocationAsync(name, funds, ct);
                model.SetUpvestAllocation(allocationId);
            });
            await _models.AddAsync(model, ct);
        }
        else
        {
            model.Update(name, fundEntities);
            await BestEffortAsync("replace allocation", async () =>
            {
                if (model.UpvestAllocationId is not null)
                {
                    await _upvest.ReplaceAllocationAsync(model.UpvestAllocationId, name, funds, ct);
                }
                else
                {
                    var allocationId = await _upvest.CreateAllocationAsync(name, funds, ct);
                    model.SetUpvestAllocation(allocationId);
                }
            });
            await _models.UpdateAsync(model, ct);
        }

        return ToView(model);
    }

    public async Task<IReadOnlyList<ModelView>> GetModelsAsync(CancellationToken ct = default)
    {
        var models = await _models.ListAsync(new ModelPortfoliosWithFundsSpecification(), ct);
        return models.Select(ToView).ToList();
    }

    public async Task ChooseModelAsync(string shopperId, int modelId, CancellationToken ct = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), ct)
            ?? throw PortfolioException.BadRequest("You are not signed up as an investor.");

        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByIdSpecification(modelId), ct)
            ?? throw PortfolioException.NotFound($"Model portfolio {modelId} does not exist.");

        var hadModel = investor.ModelId.HasValue;
        investor.ChooseModel(modelId);
        await _investors.UpdateAsync(investor, ct);

        // Link the investor's account to the model's allocation at Upvest (best-effort).
        if (investor.UpvestAccountId is not null && model.UpvestAllocationId is not null)
        {
            await BestEffortAsync("set portfolio configuration", () => hadModel
                ? _upvest.UpdateConfigurationAsync(investor.UpvestAccountId!, model.UpvestAllocationId!, ct)
                : _upvest.CreateConfigurationAsync(investor.UpvestAccountId!, model.UpvestAllocationId!, ct));
        }
    }

    // ----------------------------------------------------------------- Flow 3

    public async Task<PaymentView> PayInAsync(string shopperId, decimal amount, CancellationToken ct = default)
    {
        if (amount <= 0)
        {
            throw PortfolioException.BadRequest("Amount must be a positive number of euros.");
        }

        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), ct)
            ?? throw PortfolioException.BadRequest("You are not signed up as an investor.");

        await ReconcileInvestorAsync(investor, ct);

        if (!investor.CanPayIn)
        {
            throw PortfolioException.Conflict(
                "You can only pay in once Upvest has accepted you as an investor, your account is ready, and you have chosen a model.");
        }

        var payment = new Payment(shopperId, amount);

        try
        {
            await _upvest.FundAccountAsync(investor.UpvestAccountGroupId!, amount, Eur, ct);
            var order = await _upvest.CreatePortfolioOrderAsync(investor.UpvestUserId!, investor.UpvestAccountId!, amount, Eur, ct);
            payment.SetUpvestOrder(order.Id);
            payment.SetStatus(MapOrderStatus(order.Status));
            _logger.LogInformation("Portfolio order placed for pay-in (status {Status}).", order.Status);
        }
        catch (UpvestApiException ex)
        {
            _logger.LogWarning("Pay-in could not be placed at Upvest (HTTP {Status}); left pending.", ex.StatusCode);
        }

        await _payments.AddAsync(payment, ct);
        return ToView(payment);
    }

    public async Task<IReadOnlyList<PaymentView>> GetPaymentsAsync(string shopperId, CancellationToken ct = default)
    {
        var payments = await _payments.ListAsync(new PaymentsByShopperSpecification(shopperId), ct);

        foreach (var payment in payments.Where(p => p.Status == PaymentStatus.Pending && p.UpvestOrderId is not null))
        {
            await BestEffortAsync("refresh payment", async () =>
            {
                var order = await _upvest.GetPortfolioOrderAsync(payment.UpvestOrderId!, ct);
                var mapped = MapOrderStatus(order.Status);
                if (mapped != payment.Status)
                {
                    payment.SetStatus(mapped);
                    await _payments.UpdateAsync(payment, ct);
                }
            });
        }

        return payments.Select(ToView).ToList();
    }

    // ----------------------------------------------------------------- Flow 4

    public async Task<RealignmentView> RealignAsync(int modelId, CancellationToken ct = default)
    {
        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByIdSpecification(modelId), ct)
            ?? throw PortfolioException.NotFound($"Model portfolio {modelId} does not exist.");

        var realignment = new Realignment(modelId);

        if (model.UpvestAllocationId is not null)
        {
            try
            {
                var executionId = await _upvest.CreateRebalancingAsync(model.UpvestAllocationId, ct);
                realignment.SetUpvestExecution(executionId);
                _logger.LogInformation("Rebalancing execution triggered for model {ModelId}.", modelId);
            }
            catch (UpvestApiException ex)
            {
                _logger.LogWarning("Realignment could not be triggered at Upvest (HTTP {Status}); left pending.", ex.StatusCode);
            }
        }
        else
        {
            _logger.LogWarning("Model {ModelId} has no Upvest allocation yet; realignment left pending.", modelId);
        }

        await _realignments.AddAsync(realignment, ct);
        return ToView(realignment);
    }

    public async Task<RealignmentView> GetRealignmentAsync(Guid realignmentId, CancellationToken ct = default)
    {
        var realignment = await _realignments.FirstOrDefaultAsync(new RealignmentByPublicIdSpecification(realignmentId), ct)
            ?? throw PortfolioException.NotFound("Realignment not found.");

        if (realignment.Status == RealignmentStatus.Pending && realignment.UpvestExecutionId is not null)
        {
            await BestEffortAsync("refresh realignment", async () =>
            {
                var statuses = await _upvest.GetRebalancingOrderStatusesAsync(realignment.UpvestExecutionId!, ct);
                var mapped = MapRealignmentStatus(statuses);
                if (mapped != realignment.Status)
                {
                    realignment.SetStatus(mapped);
                    await _realignments.UpdateAsync(realignment, ct);
                }
            });
        }

        return ToView(realignment);
    }

    // ----------------------------------------------------------------- Flow 5

    public async Task<PortfolioView> GetPortfolioAsync(string shopperId, CancellationToken ct = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), ct)
            ?? throw PortfolioException.NotFound("You are not signed up as an investor.");

        var holdings = new List<HoldingView>();
        decimal totalValue = 0m;

        if (investor.UpvestAccountId is not null)
        {
            await BestEffortAsync("read valuation", async () =>
            {
                var valuation = await _upvest.GetValuationAsync(investor.UpvestAccountId!, ct);
                holdings = valuation.Positions
                    .Select(p => new HoldingView(p.InstrumentId, p.Quantity, Money(p.Value)))
                    .ToList();
                totalValue = Money(valuation.TotalValue);
            });
        }

        var payments = await _payments.ListAsync(new PaymentsByShopperSpecification(shopperId), ct);
        var paidIn = payments.Where(p => p.Status != PaymentStatus.Failed).Sum(p => p.Amount);

        return new PortfolioView(investor.ModelId, holdings, totalValue, Money(paidIn));
    }

    // --------------------------------------------------------------- Helpers

    private async Task ReconcileInvestorAsync(Investor investor, CancellationToken ct)
    {
        if (investor.UpvestUserId is null) return;

        var changed = await ApplyUpvestStatusAsync(investor, ct);
        if (changed)
        {
            await _investors.UpdateAsync(investor, ct);
        }
    }

    /// <summary>
    /// Re-reads user (and account) state from Upvest and recomputes the investor status. If Upvest
    /// will not let us read the user (e.g. the sandbox withholds read scopes), the previously known
    /// status is kept rather than being downgraded on missing information.
    /// </summary>
    private async Task<bool> ApplyUpvestStatusAsync(Investor investor, CancellationToken ct)
    {
        string? userStatus = null;
        var userRead = false;
        try
        {
            var user = await _upvest.GetUserAsync(investor.UpvestUserId!, ct);
            userStatus = user.Status;
            userRead = true;
        }
        catch (UpvestApiException ex)
        {
            _logger.LogWarning("Could not read user status from Upvest (HTTP {Status}); keeping last known status.", ex.StatusCode);
        }

        if (!userRead)
        {
            return false;
        }

        string? accountStatus = null;
        if (investor.UpvestAccountId is not null)
        {
            try
            {
                var account = await _upvest.GetAccountAsync(investor.UpvestAccountId!, ct);
                accountStatus = account.Status;
            }
            catch (UpvestApiException ex)
            {
                _logger.LogWarning("Could not read account status from Upvest (HTTP {Status}).", ex.StatusCode);
            }
        }

        var previous = investor.Status;
        var computed = ComputeInvestorStatus(investor, userStatus, accountStatus);
        investor.SetStatus(computed);
        return computed != previous;
    }

    private static InvestorStatus ComputeInvestorStatus(Investor investor, string? userStatus, string? accountStatus)
    {
        if (IsRejected(userStatus)) return InvestorStatus.Rejected;

        var userReady = string.Equals(userStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase);
        var accountReady = investor.UpvestAccountId is not null
            && string.Equals(accountStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase);

        return userReady && accountReady ? InvestorStatus.Active : InvestorStatus.Pending;
    }

    private static bool IsRejected(string? status) =>
        status is not null &&
        (status.Contains("REJECT", StringComparison.OrdinalIgnoreCase)
         || status.Contains("BLOCK", StringComparison.OrdinalIgnoreCase)
         || status.Contains("DECLIN", StringComparison.OrdinalIgnoreCase));

    private static PaymentStatus MapOrderStatus(string status) => status?.ToUpperInvariant() switch
    {
        "FILLED" => PaymentStatus.Invested,
        "PARTIALLY_CANCELLED" => PaymentStatus.Invested,
        "CANCELLED" => PaymentStatus.Failed,
        _ => PaymentStatus.Pending
    };

    private static RealignmentStatus MapRealignmentStatus(IReadOnlyList<string> orderStatuses)
    {
        if (orderStatuses.Count == 0) return RealignmentStatus.Pending;

        var anyPending = orderStatuses.Any(s =>
            s.Equals("NEW", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("PROCESSING", StringComparison.OrdinalIgnoreCase));

        return anyPending ? RealignmentStatus.Pending : RealignmentStatus.Completed;
    }

    private async Task BestEffortAsync(string what, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (UpvestApiException ex)
        {
            _logger.LogWarning("Upvest step '{What}' could not complete (HTTP {Status}).", what, ex.StatusCode);
        }
    }

    private static InvestorView ToView(Investor i) =>
        new(i.PublicId, i.Status.ToString().ToLowerInvariant());

    private static ModelView ToView(ModelPortfolio m) =>
        new(m.Id, m.Name, m.Funds.Select(f => new ModelFundView(f.InstrumentId, f.Weight)).ToList());

    private static PaymentView ToView(Payment p) =>
        new(p.PublicId, Money(p.Amount), p.Status.ToString().ToLowerInvariant());

    /// <summary>Money is reported in euros to two decimal places.</summary>
    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static RealignmentView ToView(Realignment r) =>
        new(r.PublicId, r.Status.ToString().ToLowerInvariant());
}
