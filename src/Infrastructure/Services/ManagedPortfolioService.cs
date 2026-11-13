using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.Infrastructure.Upvest;
using Microsoft.eShopWeb.Infrastructure.Upvest.Models;

namespace Microsoft.eShopWeb.Infrastructure.Services;

/// <summary>
/// Default <see cref="IManagedPortfolioService"/>. Persists the local view of investors, models,
/// payments and realignments, and drives Upvest for every externally-observable effect. Statuses
/// returned to callers are refreshed from Upvest so they reflect Upvest's reality.
/// </summary>
public class ManagedPortfolioService : IManagedPortfolioService
{
    private const string Eur = "EUR";

    private readonly IRepository<Investor> _investors;
    private readonly IRepository<ModelPortfolio> _models;
    private readonly IRepository<InvestorPayment> _payments;
    private readonly IRepository<Realignment> _realignments;
    private readonly IUpvestClient _upvest;
    private readonly IAppLogger<ManagedPortfolioService> _logger;

    public ManagedPortfolioService(
        IRepository<Investor> investors,
        IRepository<ModelPortfolio> models,
        IRepository<InvestorPayment> payments,
        IRepository<Realignment> realignments,
        IUpvestClient upvest,
        IAppLogger<ManagedPortfolioService> logger)
    {
        _investors = investors;
        _models = models;
        _payments = payments;
        _realignments = realignments;
        _upvest = upvest;
        _logger = logger;
    }

    // ---------------------------------------------------------------- Flow 1

    public async Task<InvestorView> SignUpInvestorAsync(string shopperId, InvestorSignUp signUp, CancellationToken ct)
    {
        var existing = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), ct);
        if (existing is not null)
        {
            // Idempotent: the shopper is already signing up / signed up. Report current status.
            await RefreshInvestorStatusAsync(existing, ct);
            return ToInvestorView(existing);
        }

        var investor = new Investor(shopperId);
        investor = await _investors.AddAsync(investor, ct);

        var createResult = await _upvest.CreateUserAsync(BuildCreateUserRequest(signUp), ct);
        if (createResult.IsSuccess && createResult.Value is not null)
        {
            investor.SetUpvestUser(createResult.Value.Id);
            await _investors.UpdateAsync(investor, ct);

            // Best-effort KYC check — Upvest activates the user asynchronously once checks pass.
            await TrySubmitCheckAsync(investor, signUp, ct);

            // Best-effort account setup so the investor can hold a portfolio.
            var accountReady = await TryProvisionAccountAsync(investor, ct);

            // The create response already carries the user's status, so record it directly
            // (reading the user back may need a scope the client does not hold).
            investor.UpdateStatus(ComputeInvestorStatus(createResult.Value.Status, accountReady));
            await _investors.UpdateAsync(investor, ct);
        }
        else
        {
            _logger.LogWarning("Upvest user creation for a shopper did not succeed: {Status}.", createResult.StatusCode);
            investor.UpdateStatus(InvestorStatus.Pending);
            await _investors.UpdateAsync(investor, ct);
        }

        return ToInvestorView(investor);
    }

    public async Task<InvestorView?> GetInvestorAsync(string shopperId, CancellationToken ct)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), ct);
        if (investor is null)
        {
            return null;
        }

        await RefreshInvestorStatusAsync(investor, ct);
        return ToInvestorView(investor);
    }

    private CreateUserRequest BuildCreateUserRequest(InvestorSignUp s)
    {
        var address = new UpvestAddress(
            AddressLine1: s.Address.Line1,
            Postcode: s.Address.Postcode,
            City: s.Address.City,
            Country: s.Address.Country);

        return new CreateUserRequest(
            FirstName: s.FirstName,
            LastName: s.LastName,
            Email: s.Email,
            BirthDate: s.BirthDate,
            Nationalities: new[] { s.Nationality },
            Address: address,
            Fatca: new UpvestFatca(Status: false, ConfirmedAt: UtcNowIso()),
            BirthCity: s.BirthCity,
            BirthCountry: s.BirthCountry,
            PhoneNumber: SanitizePhone(s.PhoneNumber));
    }

    /// <summary>Upvest requires 8–15 digits with no separators; strip anything else, omit if invalid.</summary>
    private static string? SanitizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length is >= 8 and <= 15 ? digits : null;
    }

    private async Task TrySubmitCheckAsync(Investor investor, InvestorSignUp signUp, CancellationToken ct)
    {
        if (investor.UpvestUserId is null)
        {
            return;
        }

        var check = new CreateCheckRequest(
            Type: "KYC",
            CheckConfirmedAt: UtcNowIso(),
            DocumentType: MapDocumentType(signUp.IdentityDocument.Type),
            DocumentExpirationDate: signUp.IdentityDocument.ExpiryDate,
            Nationality: signUp.Nationality);

        var result = await _upvest.SubmitCheckAsync(investor.UpvestUserId, check, ct);
        if (!result.IsSuccess)
        {
            _logger.LogWarning("Upvest KYC check submission not completed: {Status}.", result.StatusCode);
        }
    }

    /// <summary>Returns true once the investor has an Upvest account that is ACTIVE (ready to hold a portfolio).</summary>
    private async Task<bool> TryProvisionAccountAsync(Investor investor, CancellationToken ct)
    {
        if (investor.UpvestUserId is null)
        {
            return false;
        }

        if (investor.UpvestAccountGroupId is null)
        {
            var groupResult = await _upvest.CreateAccountGroupAsync(
                new CreateAccountGroupRequest(investor.UpvestUserId, "PERSONAL"), ct);
            if (groupResult.IsSuccess && groupResult.Value is not null)
            {
                investor.SetUpvestAccountGroup(groupResult.Value.Id);
                await _investors.UpdateAsync(investor, ct);
            }
            else
            {
                _logger.LogWarning("Upvest account group creation not completed: {Status}.", groupResult.StatusCode);
                return false;
            }
        }

        if (investor.UpvestAccountId is null && investor.UpvestAccountGroupId is not null)
        {
            var accountResult = await _upvest.CreateAccountAsync(
                new CreateAccountRequest(investor.UpvestUserId, investor.UpvestAccountGroupId, "TRADING", "Managed portfolio"), ct);
            if (accountResult.IsSuccess && accountResult.Value is not null)
            {
                investor.SetUpvestAccount(accountResult.Value.Id);
                await _investors.UpdateAsync(investor, ct);
                return string.Equals(accountResult.Value.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase);
            }

            _logger.LogWarning("Upvest account creation not completed: {Status}.", accountResult.StatusCode);
            return false;
        }

        return false;
    }

    /// <summary>
    /// Re-query Upvest and update the investor's status to reflect Upvest's reality. If Upvest
    /// cannot be read (e.g. the client lacks the read scope), the last known status is kept.
    /// </summary>
    private async Task RefreshInvestorStatusAsync(Investor investor, CancellationToken ct)
    {
        if (investor.UpvestUserId is null)
        {
            investor.UpdateStatus(InvestorStatus.Pending);
            await _investors.UpdateAsync(investor, ct);
            return;
        }

        var userResult = await _upvest.GetUserAsync(investor.UpvestUserId, ct);
        if (!userResult.IsSuccess || userResult.Value is null)
        {
            // Could not confirm with Upvest right now — keep the last known status.
            return;
        }

        if (MapUserStatus(userResult.Value.Status) == InvestorStatus.Rejected)
        {
            investor.UpdateStatus(InvestorStatus.Rejected);
            await _investors.UpdateAsync(investor, ct);
            return;
        }

        var accountReady = false;
        if (investor.UpvestAccountId is not null)
        {
            var accountResult = await _upvest.GetAccountAsync(investor.UpvestAccountId, ct);
            accountReady = accountResult.IsSuccess
                && string.Equals(accountResult.Value?.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase);
        }

        investor.UpdateStatus(ComputeInvestorStatus(userResult.Value.Status, accountReady));
        await _investors.UpdateAsync(investor, ct);
    }

    private static InvestorStatus ComputeInvestorStatus(string? upvestUserStatus, bool accountReady)
    {
        var userStatus = MapUserStatus(upvestUserStatus);
        if (userStatus == InvestorStatus.Rejected)
        {
            return InvestorStatus.Rejected;
        }

        return userStatus == InvestorStatus.Active && accountReady
            ? InvestorStatus.Active
            : InvestorStatus.Pending;
    }

    // ---------------------------------------------------------------- Flow 2

    public async Task<ModelView> PutModelAsync(int modelId, ModelDefinition definition, CancellationToken ct)
    {
        var funds = definition.Funds.Select(f => new ModelFund(f.InstrumentId, f.Weight)).ToList();

        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), ct);
        if (model is null)
        {
            model = new ModelPortfolio(modelId, definition.Name, funds); // validates weights sum to 100
            model = await _models.AddAsync(model, ct);
        }
        else
        {
            model.Replace(definition.Name, funds); // validates weights sum to 100
            await _models.UpdateAsync(model, ct);
        }

        await TrySyncAllocationAsync(model, ct);

        return ToModelView(model);
    }

    public async Task<IReadOnlyList<ModelView>> ListModelsAsync(CancellationToken ct)
    {
        var models = await _models.ListAsync(new ModelPortfoliosWithFundsSpecification(), ct);
        return models.Select(ToModelView).ToList();
    }

    public async Task<PortfolioOperationResult<ChosenModelView>> ChooseModelAsync(string shopperId, int modelId, CancellationToken ct)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), ct);
        if (investor is null)
        {
            return PortfolioOperationResult<ChosenModelView>.NotEligible("You must sign up as an investor before choosing a model.");
        }

        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), ct);
        if (model is null)
        {
            return PortfolioOperationResult<ChosenModelView>.NotFound($"No model portfolio with id {modelId} exists.");
        }

        investor.SelectModel(modelId);
        await _investors.UpdateAsync(investor, ct);

        // If the investor already has an account and the model is backed by an Upvest allocation,
        // link them so future pay-ins and realignments follow this model.
        if (investor.UpvestAccountId is not null && model.UpvestAllocationId is not null)
        {
            var configResult = await _upvest.CreateConfigurationAsync(
                new CreateConfigurationRequest(investor.UpvestAccountId, model.UpvestAllocationId), ct);
            if (!configResult.IsSuccess)
            {
                _logger.LogWarning("Linking the investor's account to the model allocation did not complete: {Status}.", configResult.StatusCode);
            }
        }

        await RefreshInvestorStatusAsync(investor, ct);
        return PortfolioOperationResult<ChosenModelView>.Ok(
            new ChosenModelView(ToInvestorId(investor), StatusText(investor.Status), modelId));
    }

    private async Task TrySyncAllocationAsync(ModelPortfolio model, CancellationToken ct)
    {
        var allocation = new CreateAllocationRequest(
            Name: model.Name,
            Allocation: model.Funds.Select(f => new AllocationEntry(f.InstrumentId, "ISIN", ToWeightFraction(f.Weight))).ToList());

        if (model.UpvestAllocationId is null)
        {
            var result = await _upvest.CreateAllocationAsync(allocation, ct);
            if (result.IsSuccess && result.Value is not null)
            {
                model.SetUpvestAllocation(result.Value.Id);
                await _models.UpdateAsync(model, ct);
            }
            else
            {
                _logger.LogWarning("Upvest allocation creation for a model did not complete: {Status}.", result.StatusCode);
            }
        }
        else
        {
            var result = await _upvest.ReplaceAllocationAsync(model.UpvestAllocationId, allocation, ct);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Upvest allocation update for a model did not complete: {Status}.", result.StatusCode);
            }
        }
    }

    // ---------------------------------------------------------------- Flow 3

    public async Task<PortfolioOperationResult<PaymentView>> CreatePaymentAsync(string shopperId, decimal amount, CancellationToken ct)
    {
        if (amount <= 0)
        {
            return PortfolioOperationResult<PaymentView>.Invalid("The pay-in amount must be greater than zero.");
        }

        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), ct);
        if (investor is null)
        {
            return PortfolioOperationResult<PaymentView>.NotEligible("You must sign up as an investor before paying in.");
        }

        await RefreshInvestorStatusAsync(investor, ct);

        if (investor.Status != InvestorStatus.Active || investor.UpvestAccountId is null)
        {
            return PortfolioOperationResult<PaymentView>.NotEligible(
                "Upvest has not yet accepted you as an investor with a ready account, so you cannot pay in.");
        }

        if (investor.SelectedModelId is null)
        {
            return PortfolioOperationResult<PaymentView>.NotEligible("You must choose a model portfolio before paying in.");
        }

        var payment = new InvestorPayment(shopperId, amount, Eur);
        payment = await _payments.AddAsync(payment, ct);

        await ExecutePayInAsync(investor, payment, amount, ct);
        await _payments.UpdateAsync(payment, ct);

        return PortfolioOperationResult<PaymentView>.Ok(ToPaymentView(payment));
    }

    private async Task ExecutePayInAsync(Investor investor, InvestorPayment payment, decimal amount, CancellationToken ct)
    {
        var cashAmount = ToMoney(amount);

        // Credit the investor's account at Upvest (sandbox funding via top-up).
        if (investor.UpvestAccountGroupId is not null)
        {
            var topupResult = await _upvest.CreateTopupAsync(
                new CreateTopupRequest(investor.UpvestAccountGroupId, cashAmount, Eur), ct);
            if (topupResult.IsSuccess && topupResult.Value is not null)
            {
                payment.SetTopup(topupResult.Value.Id);
            }
            else
            {
                payment.MarkFailed($"Could not credit the account at Upvest ({topupResult.ErrorSummary}).");
                return;
            }
        }

        // Invest the cash across the model's funds via a BUY portfolio order.
        var orderResult = await _upvest.CreatePortfolioOrderAsync(
            new CreatePortfolioOrderRequest(investor.UpvestUserId!, investor.UpvestAccountId!, cashAmount, Eur, "BUY"), ct);

        if (orderResult.IsSuccess && orderResult.Value is not null)
        {
            payment.SetPortfolioOrder(orderResult.Value.Id);
            payment.UpdateStatus(MapOrderStatus(orderResult.Value.Status));
        }
        else
        {
            payment.MarkFailed($"Could not invest the pay-in at Upvest ({orderResult.ErrorSummary}).");
        }
    }

    public async Task<IReadOnlyList<PaymentView>> ListPaymentsAsync(string shopperId, CancellationToken ct)
    {
        var payments = await _payments.ListAsync(new PaymentsByShopperSpecification(shopperId), ct);

        foreach (var payment in payments.Where(p => p.Status == PaymentStatus.Pending && p.UpvestPortfolioOrderId is not null))
        {
            var orderResult = await _upvest.GetPortfolioOrderAsync(payment.UpvestPortfolioOrderId!, ct);
            if (orderResult.IsSuccess && orderResult.Value is not null)
            {
                payment.UpdateStatus(MapOrderStatus(orderResult.Value.Status));
                await _payments.UpdateAsync(payment, ct);
            }
        }

        return payments.Select(ToPaymentView).ToList();
    }

    // ---------------------------------------------------------------- Flow 4

    public async Task<PortfolioOperationResult<RealignmentView>> CreateRealignmentAsync(int modelId, CancellationToken ct)
    {
        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), ct);
        if (model is null)
        {
            return PortfolioOperationResult<RealignmentView>.NotFound($"No model portfolio with id {modelId} exists.");
        }

        var realignment = new Realignment(modelId);
        realignment = await _realignments.AddAsync(realignment, ct);

        // Ensure the model is backed by an Upvest allocation before rebalancing against it.
        if (model.UpvestAllocationId is null)
        {
            await TrySyncAllocationAsync(model, ct);
        }

        if (model.UpvestAllocationId is null)
        {
            realignment.MarkFailed("The model is not yet backed by an Upvest allocation, so it cannot be realigned.");
            await _realignments.UpdateAsync(realignment, ct);
            return PortfolioOperationResult<RealignmentView>.Ok(ToRealignmentView(realignment));
        }

        var result = await _upvest.CreateRebalancingAsync(
            new CreateRebalancingRequest(new[] { model.UpvestAllocationId }), ct);

        if (result.IsSuccess && result.Value is not null)
        {
            realignment.SetExecution(result.Value.Id);
            realignment.UpdateStatus(RealignmentStatus.Pending);
        }
        else
        {
            realignment.MarkFailed($"Could not trigger the realignment at Upvest ({result.ErrorSummary}).");
        }

        await _realignments.UpdateAsync(realignment, ct);
        return PortfolioOperationResult<RealignmentView>.Ok(ToRealignmentView(realignment));
    }

    public async Task<RealignmentView?> GetRealignmentAsync(int realignmentId, CancellationToken ct)
    {
        var realignment = await _realignments.GetByIdAsync(realignmentId, ct);
        if (realignment is null)
        {
            return null;
        }

        if (realignment.Status == RealignmentStatus.Pending && realignment.UpvestExecutionId is not null)
        {
            var ordersResult = await _upvest.GetRebalancingOrdersAsync(realignment.UpvestExecutionId, ct);
            if (ordersResult.IsSuccess && ordersResult.Value?.Data is not null)
            {
                realignment.UpdateStatus(DeriveRealignmentStatus(ordersResult.Value.Data));
                await _realignments.UpdateAsync(realignment, ct);
            }
        }

        return ToRealignmentView(realignment);
    }

    // ---------------------------------------------------------------- Flow 5

    public async Task<PortfolioView?> GetPortfolioAsync(string shopperId, CancellationToken ct)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), ct);
        if (investor is null)
        {
            return null;
        }

        var payments = await _payments.ListAsync(new PaymentsByShopperSpecification(shopperId), ct);
        var paidIn = payments
            .Where(p => p.Status != PaymentStatus.Failed)
            .Sum(p => p.Amount);

        var holdings = new List<HoldingView>();
        decimal totalValue = 0m;

        if (investor.UpvestAccountId is not null)
        {
            var valuationResult = await _upvest.GetCurrentValuationAsync(investor.UpvestAccountId, ct);
            if (valuationResult.IsSuccess && valuationResult.Value is not null)
            {
                var valuation = valuationResult.Value;
                totalValue = ParseMoney(valuation.TotalSecurityValue?.Amount);

                foreach (var position in valuation.SecurityPositions ?? Enumerable.Empty<UpvestSecurityPosition>())
                {
                    var isin = position.Instrument?.Isin;
                    if (string.IsNullOrEmpty(isin))
                    {
                        continue;
                    }

                    holdings.Add(new HoldingView(
                        InstrumentId: isin,
                        Quantity: ParseDecimal(position.Quantity),
                        Value: ParseMoney(position.Value?.Amount)));
                }
            }
            else
            {
                _logger.LogWarning("Could not retrieve the investor's valuation from Upvest: {Status}.", valuationResult.StatusCode);
            }
        }

        return new PortfolioView(
            ModelId: investor.SelectedModelId,
            Holdings: holdings,
            TotalValue: Round2(totalValue),
            PaidIn: Round2(paidIn));
    }

    // ---------------------------------------------------------------- Mapping helpers

    private static InvestorView ToInvestorView(Investor investor) =>
        new(ToInvestorId(investor), StatusText(investor.Status));

    private static string ToInvestorId(Investor investor) =>
        investor.UpvestUserId ?? investor.Id.ToString(CultureInfo.InvariantCulture);

    private static ModelView ToModelView(ModelPortfolio model) =>
        new(model.ModelId, model.Name,
            model.Funds.Select(f => new FundView(f.InstrumentId, Round2OrExact(f.Weight))).ToList());

    private static PaymentView ToPaymentView(InvestorPayment payment) =>
        new(payment.Id.ToString(CultureInfo.InvariantCulture), Round2(payment.Amount), StatusText(payment.Status));

    private static RealignmentView ToRealignmentView(Realignment realignment) =>
        new(realignment.Id.ToString(CultureInfo.InvariantCulture), StatusText(realignment.Status));

    private static string StatusText(InvestorStatus status) => status.ToString().ToLowerInvariant();
    private static string StatusText(PaymentStatus status) => status.ToString().ToLowerInvariant();
    private static string StatusText(RealignmentStatus status) => status.ToString().ToLowerInvariant();

    private static InvestorStatus MapUserStatus(string? upvestStatus) => (upvestStatus?.ToUpperInvariant()) switch
    {
        "ACTIVE" => InvestorStatus.Active,
        "REJECTED" or "BLOCKED" or "DEACTIVATED" or "CLOSED" or "DECEASED" => InvestorStatus.Rejected,
        _ => InvestorStatus.Pending
    };

    private static PaymentStatus MapOrderStatus(string? upvestStatus) => (upvestStatus?.ToUpperInvariant()) switch
    {
        "FILLED" or "SETTLED" => PaymentStatus.Invested,
        "CANCELLED" => PaymentStatus.Failed,
        _ => PaymentStatus.Pending
    };

    private static RealignmentStatus DeriveRealignmentStatus(IReadOnlyList<RebalancingExecutionOrder> orders)
    {
        if (orders.Count == 0)
        {
            return RealignmentStatus.Pending;
        }

        var anyPending = orders.Any(o => o.Status?.ToUpperInvariant() is "NEW" or "PROCESSING");
        if (anyPending)
        {
            return RealignmentStatus.Pending;
        }

        var anyFilled = orders.Any(o => o.Status?.ToUpperInvariant() is "FILLED" or "SETTLED");
        var anyBadCancel = orders.Any(o =>
            o.Status?.ToUpperInvariant() == "CANCELLED"
            && !string.Equals(o.CancellationReason, "PORTFOLIO_IS_BALANCED", StringComparison.OrdinalIgnoreCase));

        if (anyBadCancel && !anyFilled)
        {
            return RealignmentStatus.Failed;
        }

        return RealignmentStatus.Completed;
    }

    private static string MapDocumentType(string type)
    {
        var normalized = type.Trim().ToUpperInvariant().Replace('-', '_').Replace(' ', '_');
        return normalized switch
        {
            "PASSPORT" => "PASSPORT",
            "ID_CARD" or "IDCARD" or "NATIONAL_ID" or "IDENTITY_CARD" => "ID_CARD",
            "DRIVERS_LICENSE" or "DRIVING_LICENSE" or "DRIVERS_LICENCE" => "DRIVERS_LICENSE",
            "RESIDENCE_PERMIT" => "RESIDENCE_PERMIT",
            _ => normalized
        };
    }

    private static string UtcNowIso() => DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private static string ToMoney(decimal amount) => Round2(amount).ToString("0.00", CultureInfo.InvariantCulture);

    private static string ToWeightFraction(decimal percentage) =>
        (percentage / 100m).ToString("0.############", CultureInfo.InvariantCulture);

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal Round2OrExact(decimal value) => value;

    private static decimal ParseMoney(string? amount) => ParseDecimal(amount);

    private static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
}
