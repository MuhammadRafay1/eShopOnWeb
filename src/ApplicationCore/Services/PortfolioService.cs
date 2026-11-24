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
using Microsoft.eShopWeb.ApplicationCore.Upvest;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Orchestrates the managed-portfolio flows: it keeps a local record that maps each shopper to the
/// entities Upvest holds for them, drives the Upvest gateway, and reports statuses that reflect what
/// Upvest records (learned by polling Upvest on read). Reads degrade gracefully when a scope is not
/// authorised, so a flow can be driven as far as the environment allows without failing the caller.
/// </summary>
public class PortfolioService : IPortfolioService
{
    private static readonly HashSet<string> BenignCancellations = new(StringComparer.OrdinalIgnoreCase)
    {
        "PORTFOLIO_IS_BALANCED",
        "ACCOUNT_IS_EMPTY"
    };

    private readonly IUpvestClient _upvest;
    private readonly IRepository<Investor> _investors;
    private readonly IRepository<Payment> _payments;
    private readonly IRepository<ModelPortfolio> _models;
    private readonly IRepository<Realignment> _realignments;
    private readonly IAppLogger<PortfolioService> _logger;

    public PortfolioService(
        IUpvestClient upvest,
        IRepository<Investor> investors,
        IRepository<Payment> payments,
        IRepository<ModelPortfolio> models,
        IRepository<Realignment> realignments,
        IAppLogger<PortfolioService> logger)
    {
        _upvest = upvest;
        _investors = investors;
        _payments = payments;
        _models = models;
        _realignments = realignments;
        _logger = logger;
    }

    // ----------------------------------------------- Flow 1: becoming an investor

    public async Task<InvestorView> SignUpInvestorAsync(string shopperId, InvestorSignUp form, CancellationToken ct = default)
    {
        var existing = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), ct);
        if (existing is not null)
        {
            await RefreshInvestorStatusAsync(existing, ct);
            return ToView(existing);
        }

        var investor = new Investor(shopperId);
        await _investors.AddAsync(investor, ct);

        // Create the underlying Upvest user (this is the operation the sandbox authorises).
        var createRequest = new UpvestCreateUserRequest(
            form.FirstName,
            form.LastName,
            string.IsNullOrWhiteSpace(form.Email) ? null : form.Email,
            form.BirthDate,
            form.BirthCity,
            form.BirthCountry,
            form.Nationality,
            new UpvestAddress(form.Address.Line1, form.Address.Postcode, form.Address.City, form.Address.Country),
            form.PhoneNumber);

        var user = await _upvest.CreateUserAsync(createRequest, ct);
        investor.SetUpvestUser(user.Id);
        _logger.LogInformation("Created Upvest user for an investor sign-up (userId {UserId}).", user.Id);

        // Register the identity document the shop verified as a KYC check (best-effort).
        await TryStepAsync("register KYC check", async () =>
        {
            var kyc = new UpvestKycCheckRequest(
                MapDocumentType(form.IdentityDocument.Type),
                form.IdentityDocument.IssuingCountry,
                form.IdentityDocument.ExpiryDate,
                new UpvestAddress(form.Address.Line1, form.Address.Postcode, form.Address.City, form.Address.Country));
            var checkId = await _upvest.CreateKycCheckAsync(user.Id, kyc, ct);
            investor.SetUpvestKycCheck(checkId);
        });

        // Provision the account group and the portfolio account (best-effort).
        await TryStepAsync("provision portfolio account", async () =>
        {
            var group = await _upvest.CreateAccountGroupAsync(user.Id, ct);
            var account = await _upvest.CreateAccountAsync(user.Id, group.Id, ct);
            investor.SetUpvestAccount(group.Id, account.Id);
        });

        await RefreshInvestorStatusAsync(investor, ct);
        await _investors.UpdateAsync(investor, ct);
        return ToView(investor);
    }

    public async Task<InvestorView?> GetInvestorAsync(string shopperId, CancellationToken ct = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), ct);
        if (investor is null) return null;

        await RefreshInvestorStatusAsync(investor, ct);
        await _investors.UpdateAsync(investor, ct);
        return ToView(investor);
    }

    private async Task RefreshInvestorStatusAsync(Investor investor, CancellationToken ct)
    {
        var status = InvestorStatus.Pending;

        if (investor.UpvestKycCheckId is not null && investor.UpvestUserId is not null)
        {
            var check = await TryQueryAsync(() => _upvest.GetCheckAsync(investor.UpvestUserId!, investor.UpvestKycCheckId!, ct));
            if (check is not null && check.Status.Equals("FAILED", StringComparison.OrdinalIgnoreCase))
                status = InvestorStatus.Rejected;
        }

        if (status != InvestorStatus.Rejected && investor.UpvestUserId is not null)
        {
            var user = await TryQueryAsync(() => _upvest.GetUserAsync(investor.UpvestUserId!, ct));
            if (user is not null &&
                (user.Status.Equals("OFFBOARDED", StringComparison.OrdinalIgnoreCase) ||
                 user.Status.Equals("OFFBOARDING", StringComparison.OrdinalIgnoreCase)))
            {
                status = InvestorStatus.Rejected;
            }
        }

        if (status != InvestorStatus.Rejected && investor.UpvestAccountId is not null)
        {
            var account = await TryQueryAsync(() => _upvest.GetAccountAsync(investor.UpvestAccountId!, ct));
            if (account is not null && account.Status.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase))
                status = InvestorStatus.Active;
        }

        investor.UpdateStatus(status);
    }

    // ----------------------------------------------------- Flow 2: model portfolios

    public async Task<ModelView> UpsertModelAsync(string modelId, string name, IReadOnlyList<ModelFundInput> funds, CancellationToken ct = default)
    {
        var fundEntities = funds.Select(f => new ModelFund(f.InstrumentId, f.Weight)).ToList();

        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), ct);
        bool isNew = model is null;
        try
        {
            if (model is null)
                model = new ModelPortfolio(modelId, name, fundEntities);
            else
                model.SetDefinition(name, fundEntities);
        }
        catch (ArgumentException ex)
        {
            throw new PortfolioValidationException(ex.Message);
        }

        // Back the model with a portfolio allocation at Upvest (best-effort).
        var entries = funds.Select(f => new UpvestAllocationEntry(f.InstrumentId, f.Weight)).ToList();
        await TryStepAsync("sync model allocation", async () =>
        {
            if (model!.UpvestAllocationId is null)
            {
                var allocation = await _upvest.CreateAllocationAsync(name, entries, ct);
                model.SetUpvestAllocation(allocation.Id);
            }
            else
            {
                await _upvest.UpdateAllocationAsync(model.UpvestAllocationId, name, entries, ct);
            }
        });

        if (isNew)
            await _models.AddAsync(model!, ct);
        else
            await _models.UpdateAsync(model!, ct);

        return ToView(model!);
    }

    public async Task<IReadOnlyList<ModelView>> GetModelsAsync(CancellationToken ct = default)
    {
        var models = await _models.ListAsync(ct);
        return models.Select(ToView).ToList();
    }

    public async Task ChooseModelAsync(string shopperId, string modelId, CancellationToken ct = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), ct);
        if (investor is null)
            throw new PortfolioValidationException("You must sign up as an investor before choosing a model.");

        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), ct);
        if (model is null)
            throw new PortfolioNotFoundException($"Model portfolio '{modelId}' does not exist.");

        bool switching = investor.ModelId is not null;
        investor.ChooseModel(modelId);
        await _investors.UpdateAsync(investor, ct);

        // Point the investor's portfolio configuration at the chosen allocation (best-effort).
        if (investor.UpvestAccountId is not null && model.UpvestAllocationId is not null)
        {
            await TryStepAsync("apply model configuration", async () =>
            {
                if (switching)
                    await _upvest.UpdateConfigurationAsync(investor.UpvestAccountId!, model.UpvestAllocationId!, ct);
                else
                    await _upvest.CreateConfigurationAsync(investor.UpvestAccountId!, model.UpvestAllocationId!, ct);
            });
        }
    }

    // ------------------------------------------------------------- Flow 3: paying in

    public async Task<PaymentView> PayInAsync(string shopperId, decimal amount, CancellationToken ct = default)
    {
        if (amount <= 0)
            throw new PortfolioValidationException("The amount to pay in must be greater than zero.");

        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), ct);
        if (investor is null)
            throw new PortfolioValidationException("You are not an investor. Sign up before paying in.");

        await RefreshInvestorStatusAsync(investor, ct);
        await _investors.UpdateAsync(investor, ct);

        if (investor.Status != InvestorStatus.Active)
            throw new PortfolioValidationException("Your investor account is not active yet. You can pay in once Upvest has accepted you.");
        if (investor.ModelId is null)
            throw new PortfolioValidationException("Choose a model portfolio before paying in.");

        var payment = new Payment(shopperId, amount, "EUR");
        await _payments.AddAsync(payment, ct);

        await TryStepAsync("invest pay-in", async () =>
        {
            // Credit the money to the account, then invest it across the model's funds.
            if (investor.UpvestAccountGroupId is not null)
                await _upvest.TopUpAsync(investor.UpvestAccountGroupId, amount, "EUR", ct);

            var order = await _upvest.CreatePortfolioOrderAsync(investor.UpvestUserId!, investor.UpvestAccountId!, amount, "EUR", ct);
            payment.SetUpvestOrder(order.Id);
            payment.UpdateStatus(MapOrderStatus(order.Status));
        });

        await _payments.UpdateAsync(payment, ct);
        return ToView(payment);
    }

    public async Task<IReadOnlyList<PaymentView>> GetPaymentsAsync(string shopperId, CancellationToken ct = default)
    {
        var payments = await _payments.ListAsync(new PaymentsByShopperSpecification(shopperId), ct);
        foreach (var payment in payments.Where(p => p.Status == PaymentStatus.Pending && p.UpvestOrderId is not null))
        {
            var order = await TryQueryAsync(() => _upvest.GetPortfolioOrderAsync(payment.UpvestOrderId!, ct));
            if (order is not null)
            {
                payment.UpdateStatus(MapOrderStatus(order.Status));
                await _payments.UpdateAsync(payment, ct);
            }
        }
        return payments.Select(ToView).ToList();
    }

    // --------------------------------------------------------- Flow 4: realignment

    public async Task<RealignmentView> StartRealignmentAsync(string modelId, CancellationToken ct = default)
    {
        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), ct);
        if (model is null)
            throw new PortfolioNotFoundException($"Model portfolio '{modelId}' does not exist.");

        var realignment = new Realignment(modelId);
        await _realignments.AddAsync(realignment, ct);

        if (model.UpvestAllocationId is not null)
        {
            await TryStepAsync("trigger realignment", async () =>
            {
                var executionId = await _upvest.TriggerRebalancingAsync(model.UpvestAllocationId!, ct);
                realignment.SetUpvestExecution(executionId);
            });
        }

        await RefreshRealignmentStatusAsync(realignment, ct);
        await _realignments.UpdateAsync(realignment, ct);
        return ToView(realignment);
    }

    public async Task<RealignmentView?> GetRealignmentAsync(string realignmentId, CancellationToken ct = default)
    {
        if (!int.TryParse(realignmentId, out var id))
            throw new PortfolioNotFoundException($"Realignment '{realignmentId}' does not exist.");

        var realignment = await _realignments.GetByIdAsync(id, ct);
        if (realignment is null)
            throw new PortfolioNotFoundException($"Realignment '{realignmentId}' does not exist.");

        await RefreshRealignmentStatusAsync(realignment, ct);
        await _realignments.UpdateAsync(realignment, ct);
        return ToView(realignment);
    }

    private async Task RefreshRealignmentStatusAsync(Realignment realignment, CancellationToken ct)
    {
        if (realignment.UpvestExecutionId is null) return;

        var execution = await TryQueryAsync(() => _upvest.GetRebalancingAsync(realignment.UpvestExecutionId!, ct));
        if (execution is null) return;

        realignment.UpdateStatus(DeriveRealignmentStatus(execution.Orders));
    }

    private static RealignmentStatus DeriveRealignmentStatus(IReadOnlyList<UpvestOrder> orders)
    {
        if (orders.Count == 0)
            return RealignmentStatus.Pending;

        if (orders.Any(o => o.Status is "NEW" or "PROCESSING"))
            return RealignmentStatus.Pending;

        bool anyBadCancellation = orders.Any(o =>
            o.Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) &&
            !(o.CancellationReason is not null && BenignCancellations.Contains(o.CancellationReason)));

        return anyBadCancellation ? RealignmentStatus.Failed : RealignmentStatus.Completed;
    }

    // ------------------------------------------------ Flow 5: the investor's portfolio

    public async Task<PortfolioView?> GetPortfolioAsync(string shopperId, CancellationToken ct = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), ct);
        if (investor is null) return null;

        var holdings = new List<HoldingView>();
        decimal totalValue = 0m;

        if (investor.UpvestAccountId is not null)
        {
            var valuation = await TryQueryAsync(() => _upvest.GetAccountValuationAsync(investor.UpvestAccountId!, ct));
            if (valuation is not null)
            {
                holdings = valuation.Holdings.Select(h => new HoldingView(h.InstrumentId, h.Quantity, h.Value)).ToList();
                totalValue = valuation.TotalValue;
            }
        }

        var payments = await _payments.ListAsync(new PaymentsByShopperSpecification(shopperId), ct);
        decimal paidIn = payments.Where(p => p.Status == PaymentStatus.Invested).Sum(p => p.Amount);

        return new PortfolioView(investor.ModelId, holdings, totalValue, paidIn);
    }

    // ------------------------------------------------------------------- Helpers

    /// <summary>Runs a best-effort Upvest step, swallowing failures so the overall flow proceeds.</summary>
    private async Task TryStepAsync(string step, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (UpvestAuthorizationException ex)
        {
            _logger.LogInformation("Upvest step '{Step}' skipped: this client is not authorised for the required scope ({Message}).", step, ex.Message);
        }
        catch (UpvestApiException ex)
        {
            _logger.LogWarning("Upvest step '{Step}' did not complete: {Message}", step, ex.Message);
        }
    }

    /// <summary>Runs a best-effort Upvest read, returning default when it cannot be completed.</summary>
    private async Task<T?> TryQueryAsync<T>(Func<Task<T?>> query)
    {
        try
        {
            return await query();
        }
        catch (UpvestAuthorizationException ex)
        {
            _logger.LogInformation("Upvest read skipped: not authorised ({Message}).", ex.Message);
            return default;
        }
        catch (UpvestApiException ex)
        {
            _logger.LogWarning("Upvest read failed: {Message}", ex.Message);
            return default;
        }
    }

    private static string MapDocumentType(string type)
    {
        var normalized = (type ?? string.Empty).Trim().ToUpperInvariant().Replace(' ', '_');
        return normalized switch
        {
            "PASSPORT" => "PASSPORT",
            "ID_CARD" or "IDENTITY_CARD" or "NATIONAL_ID" => "ID_CARD",
            "RESIDENCE_PERMIT" => "RESIDENCE_PERMIT",
            "DRIVING_LICENSE" or "DRIVING_LICENCE" or "DRIVERS_LICENSE" => "DRIVING_LICENSE",
            _ => "PASSPORT"
        };
    }

    private static PaymentStatus MapOrderStatus(string status) => status.ToUpperInvariant() switch
    {
        "FILLED" or "SETTLED" => PaymentStatus.Invested,
        "CANCELLED" => PaymentStatus.Failed,
        _ => PaymentStatus.Pending
    };

    private static InvestorView ToView(Investor investor) =>
        new(investor.UpvestUserId ?? investor.Id.ToString(), ToApiStatus(investor.Status));

    private static ModelView ToView(ModelPortfolio model) =>
        new(model.ModelId, model.Name, model.Funds.Select(f => new ModelFundInput(f.InstrumentId, f.Weight)).ToList());

    private static PaymentView ToView(Payment payment) =>
        new(payment.Id.ToString(), payment.Amount, ToApiStatus(payment.Status));

    private static RealignmentView ToView(Realignment realignment) =>
        new(realignment.Id.ToString(), ToApiStatus(realignment.Status));

    private static string ToApiStatus(InvestorStatus status) => status switch
    {
        InvestorStatus.Active => "active",
        InvestorStatus.Rejected => "rejected",
        _ => "pending"
    };

    private static string ToApiStatus(PaymentStatus status) => status switch
    {
        PaymentStatus.Invested => "invested",
        PaymentStatus.Failed => "failed",
        _ => "pending"
    };

    private static string ToApiStatus(RealignmentStatus status) => status switch
    {
        RealignmentStatus.Completed => "completed",
        RealignmentStatus.Failed => "failed",
        _ => "pending"
    };
}
