using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Orchestrates the managed-portfolio flows over the application's repositories and the Upvest
/// gateway. Keeps application state authoritative for what the shop knows (investors, models,
/// payments, realignments) and always re-reads Upvest for the status it reports. Provider failures
/// on write/best-effort paths are caught and leave the row in a reconcilable state rather than
/// failing the caller's whole request; read paths degrade to what the application already holds.
/// </summary>
public sealed class PortfolioService : IPortfolioService
{
    private const decimal MaxAmount = 999_999_999.99m;

    private readonly IRepository<PortfolioInvestor> _investors;
    private readonly IRepository<ModelPortfolio> _models;
    private readonly IRepository<PortfolioPayment> _payments;
    private readonly IRepository<PortfolioRealignment> _realignments;
    private readonly IUpvestPortfolioGateway _upvest;
    private readonly IAppLogger<PortfolioService> _logger;

    public PortfolioService(
        IRepository<PortfolioInvestor> investors,
        IRepository<ModelPortfolio> models,
        IRepository<PortfolioPayment> payments,
        IRepository<PortfolioRealignment> realignments,
        IUpvestPortfolioGateway upvest,
        IAppLogger<PortfolioService> logger)
    {
        _investors = investors;
        _models = models;
        _payments = payments;
        _realignments = realignments;
        _upvest = upvest;
        _logger = logger;
    }

    // ----------------------------------------------------------------- Flow 1: investor

    public async Task<InvestorView> SignUpInvestorAsync(string buyerId, InvestorRegistration registration, CancellationToken ct)
    {
        ValidateRegistration(registration);

        var investor = await _investors.GetByIdAsync(buyerId, ct);
        if (investor is null)
        {
            investor = new PortfolioInvestor(buyerId);
            try
            {
                // Claim: one investor row per shopper. A concurrent second sign-up conflicts on the
                // BuyerId primary key; we catch that and return the row that won.
                await _investors.AddAsync(investor, ct);
            }
            catch (Exception ex) when (IsUniquenessConflict(ex))
            {
                _logger.LogWarning("Concurrent investor sign-up detected; returning existing record.");
                investor = await _investors.GetByIdAsync(buyerId, ct)
                           ?? throw new PortfolioConflictException("Investor sign-up is in progress.");
                return ToView(investor);
            }
        }
        else
        {
            // Idempotent: a repeat sign-up for an existing shopper returns their current state.
            _logger.LogInformation("Investor {InvestorId} already signed up; reconciling.", investor.InvestorId);
        }

        await ReconcileInvestorAsync(investor, registration, ct);
        await _investors.UpdateAsync(investor, ct);
        return ToView(investor);
    }

    public async Task<InvestorView> GetInvestorAsync(string buyerId, CancellationToken ct)
    {
        var investor = await _investors.GetByIdAsync(buyerId, ct)
                       ?? throw new PortfolioNotFoundException("You have not signed up as an investor.");

        // Re-read Upvest to settle the sign-up status, resuming any provisioning that can proceed
        // without the personal details (which are only available at sign-up time).
        await ReconcileInvestorAsync(investor, registration: null, ct);
        await _investors.UpdateAsync(investor, ct);
        return ToView(investor);
    }

    /// <summary>
    /// Drives provisioning as far forward as it can and settles the status from Upvest. Each Upvest
    /// call is best-effort: a provider failure is logged and stops the sequence, leaving the row to
    /// be resumed on the next call. <paramref name="registration"/> is required to create the Upvest
    /// user and KYC check; when null (a GET), those steps are skipped and only reconciliation runs.
    /// </summary>
    private async Task ReconcileInvestorAsync(PortfolioInvestor investor, InvestorRegistration? registration, CancellationToken ct)
    {
        try
        {
            if (investor.UpvestUserId is null)
            {
                if (registration is null) return; // cannot create a user without the sign-up details
                var userId = await _upvest.CreateUserAsync(registration, investor.CreateUserIdempotencyKey, ct);
                investor.SetUpvestUserId(userId);
            }

            if (investor.UpvestAccountGroupId is null)
            {
                var groupId = await _upvest.CreateAccountGroupAsync(investor.UpvestUserId!.Value, investor.CreateAccountGroupIdempotencyKey, ct);
                investor.SetUpvestAccountGroupId(groupId);
            }

            if (investor.UpvestAccountId is null)
            {
                var account = await _upvest.CreateAccountAsync(investor.UpvestUserId!.Value, investor.UpvestAccountGroupId!.Value, investor.CreateAccountIdempotencyKey, ct);
                investor.SetUpvestAccountId(account.AccountId);
            }

            if (investor.UpvestKycCheckId is null && registration is not null)
            {
                var checkId = await _upvest.CreateKycCheckAsync(investor.UpvestUserId!.Value, registration.IdentityDocument, registration.Nationality, ct);
                investor.SetUpvestKycCheckId(checkId);
            }

            if (investor.UpvestUserId is not null && investor.UpvestAccountId is not null)
            {
                var acceptance = await _upvest.GetAcceptanceAsync(investor.UpvestUserId.Value, investor.UpvestAccountId.Value, ct);
                ApplyAcceptance(investor, acceptance);
            }
        }
        catch (UpvestGatewayException ex)
        {
            // A definite rejection of the shopper's own details at the user-creation step (before any
            // Upvest user exists) means Upvest will not take them on → rejected. Anything else — a
            // missing scope, a transport failure, or a later provisioning step — leaves the sign-up
            // pending so the next GET re-reads and settles it.
            if (investor.UpvestUserId is null && ex.StatusCode is 400 or 422)
            {
                investor.MarkRejected();
                _logger.LogWarning("Investor {InvestorId} was rejected by Upvest at sign-up (status {Status}).",
                    investor.InvestorId, ex.StatusCode);
            }
            else
            {
                _logger.LogWarning("Provisioning for investor {InvestorId} could not complete at Upvest (status {Status}, unknown={Unknown}).",
                    investor.InvestorId, ex.StatusCode, ex.OutcomeUnknown);
            }
        }
    }

    private static void ApplyAcceptance(PortfolioInvestor investor, InvestorAcceptance a)
    {
        if (a.KycFailed || a.UserOffboarded)
            investor.MarkRejected();
        else if (a.KycPassed && a.AccountActive)
            investor.MarkActive();
        else
            investor.MarkPending();
    }

    // ----------------------------------------------------------------- Flow 2: models

    public async Task<ModelView> UpsertModelAsync(string modelId, string name, IReadOnlyList<ModelFund> funds, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            throw new PortfolioValidationException("A modelId is required.");
        ValidateModel(name, funds);

        // Best-effort check that each ISIN is an instrument Upvest offers. A definite "not offered"
        // is the caller's error (400); a provider that cannot be consulted (e.g. the sandbox's
        // user-only client) is logged and does not block the operator from defining the model.
        try
        {
            await _upvest.EnsureInstrumentsExistAsync(funds.Select(f => f.InstrumentId).ToList(), ct);
        }
        catch (PortfolioValidationException)
        {
            throw;
        }
        catch (UpvestGatewayException ex)
        {
            _logger.LogWarning("Could not verify instruments for model {ModelId} at Upvest (status {Status}); proceeding.", modelId, ex.StatusCode);
        }

        var model = await _models.GetByIdAsync(modelId, ct);
        var fundEntities = funds.Select(f => new ModelPortfolioFund(f.InstrumentId, f.Weight)).ToList();
        if (model is null)
        {
            model = new ModelPortfolio(modelId, name, fundEntities);
            await _models.AddAsync(model, ct);
        }
        else
        {
            model.Replace(name, fundEntities);
        }

        // Reflect the model at Upvest as an allocation. Failures are logged; the allocation id stays
        // unset and is retried on the next upsert or realignment.
        try
        {
            if (model.UpvestAllocationId is null)
            {
                var allocationId = await _upvest.CreateModelAllocationAsync(model.Name, funds, model.CreateAllocationIdempotencyKey, ct);
                model.SetUpvestAllocationId(allocationId);
            }
            else
            {
                await _upvest.UpdateModelAllocationAsync(model.UpvestAllocationId.Value, model.Name, funds, ct);
            }
        }
        catch (UpvestGatewayException ex)
        {
            _logger.LogWarning("Could not sync model {ModelId} to an Upvest allocation (status {Status}).", modelId, ex.StatusCode);
        }

        await _models.UpdateAsync(model, ct);
        return ToView(model);
    }

    public async Task<IReadOnlyList<ModelView>> GetModelsAsync(CancellationToken ct)
    {
        var models = await _models.ListAsync(ct);
        return models.Select(ToView).ToList();
    }

    public async Task ChooseModelAsync(string buyerId, string modelId, CancellationToken ct)
    {
        var investor = await _investors.GetByIdAsync(buyerId, ct)
                       ?? throw new PortfolioNotFoundException("You have not signed up as an investor.");

        var model = await _models.GetByIdAsync(modelId, ct)
                    ?? throw new PortfolioNotFoundException($"No model portfolio '{modelId}' exists.");

        var switching = investor.SelectedModelId is not null && investor.ModelConfiguredAtUpvest;
        investor.ChooseModel(modelId);

        // Apply the choice at Upvest when both sides are ready; otherwise the selection is recorded
        // and applied the next time provisioning / the allocation catches up.
        if (investor.IsProvisioned && model.UpvestAllocationId is not null)
        {
            try
            {
                if (switching)
                    await _upvest.UpdateInvestorConfigurationAsync(investor.UpvestAccountId!.Value, model.UpvestAllocationId.Value, investor.ChooseModelIdempotencyKey, ct);
                else
                    await _upvest.CreateInvestorConfigurationAsync(investor.UpvestAccountId!.Value, model.UpvestAllocationId.Value, investor.ChooseModelIdempotencyKey, ct);
                investor.MarkModelConfigured();
            }
            catch (UpvestGatewayException ex)
            {
                _logger.LogWarning("Could not apply model choice for investor {InvestorId} at Upvest (status {Status}).", investor.InvestorId, ex.StatusCode);
            }
        }

        await _investors.UpdateAsync(investor, ct);
    }

    // ----------------------------------------------------------------- Flow 3: paying in

    public async Task<PaymentView> PayInAsync(string buyerId, decimal amount, CancellationToken ct)
    {
        var investor = await _investors.GetByIdAsync(buyerId, ct)
                       ?? throw new PortfolioNotFoundException("You have not signed up as an investor.");

        if (!investor.IsReadyToInvest || investor.SelectedModelId is null)
            throw new PortfolioConflictException(
                "You can pay in only once Upvest has accepted you as an investor and you have chosen a model.");

        ValidateAmount(amount);

        var payment = new PortfolioPayment(buyerId, amount);
        await _payments.AddAsync(payment, ct); // claim on PaymentId

        try
        {
            if (!payment.CashCredited)
            {
                await _upvest.CreditCashAsync(investor.UpvestAccountGroupId!.Value, amount, payment.CashCreditIdempotencyKey, ct);
                payment.MarkCashCredited();
            }

            var orderId = await _upvest.PlaceOrderAsync(investor.UpvestUserId!.Value, investor.UpvestAccountId!.Value, amount, payment.PlaceOrderIdempotencyKey, ct);
            payment.SetUpvestOrderId(orderId);

            var outcome = await _upvest.GetOrderOutcomeAsync(orderId, ct);
            ApplyOutcome(payment, outcome);
        }
        catch (UpvestGatewayException ex)
        {
            // Unknown or refused: leave the payment pending; GET /payments reconciles it from the
            // order once one exists. Never report a definite failure for an unknown outcome.
            _logger.LogWarning("Pay-in {PaymentId} did not settle at Upvest (status {Status}, unknown={Unknown}).",
                payment.PaymentId, ex.StatusCode, ex.OutcomeUnknown);
        }

        await _payments.UpdateAsync(payment, ct);
        return ToView(payment);
    }

    public async Task<IReadOnlyList<PaymentView>> GetPaymentsAsync(string buyerId, CancellationToken ct)
    {
        var payments = await _payments.ListAsync(new PortfolioPaymentsByBuyerSpecification(buyerId), ct);

        foreach (var payment in payments.Where(p => p.Status == PaymentStatus.Pending && p.UpvestOrderId is not null))
        {
            try
            {
                var outcome = await _upvest.GetOrderOutcomeAsync(payment.UpvestOrderId!.Value, ct);
                if (outcome != ProviderOutcome.Pending)
                {
                    ApplyOutcome(payment, outcome);
                    await _payments.UpdateAsync(payment, ct);
                }
            }
            catch (UpvestGatewayException ex)
            {
                _logger.LogWarning("Could not reconcile payment {PaymentId} (status {Status}).", payment.PaymentId, ex.StatusCode);
            }
        }

        return payments.Select(ToView).ToList();
    }

    private static void ApplyOutcome(PortfolioPayment payment, ProviderOutcome outcome)
    {
        switch (outcome)
        {
            case ProviderOutcome.Succeeded: payment.MarkInvested(); break;
            case ProviderOutcome.Failed: payment.MarkFailed(); break;
            default: payment.MarkPending(); break;
        }
    }

    // ----------------------------------------------------------------- Flow 4: realignment

    public async Task<RealignmentView> TriggerRealignmentAsync(string modelId, CancellationToken ct)
    {
        var model = await _models.GetByIdAsync(modelId, ct)
                    ?? throw new PortfolioNotFoundException($"No model portfolio '{modelId}' exists.");

        var realignment = new PortfolioRealignment(modelId);
        await _realignments.AddAsync(realignment, ct); // claim on RealignmentId

        if (model.UpvestAllocationId is not null)
        {
            try
            {
                var executionId = await _upvest.TriggerRealignmentAsync(model.UpvestAllocationId.Value, realignment.TriggerIdempotencyKey, ct);
                realignment.SetUpvestExecutionId(executionId);

                var outcome = await _upvest.GetRealignmentOutcomeAsync(executionId, ct);
                ApplyOutcome(realignment, outcome);
            }
            catch (UpvestGatewayException ex)
            {
                _logger.LogWarning("Realignment {RealignmentId} did not settle at Upvest (status {Status}, unknown={Unknown}).",
                    realignment.RealignmentId, ex.StatusCode, ex.OutcomeUnknown);
            }
        }
        else
        {
            _logger.LogWarning("Model {ModelId} has no Upvest allocation yet; realignment {RealignmentId} stays pending.", modelId, realignment.RealignmentId);
        }

        await _realignments.UpdateAsync(realignment, ct);
        return ToView(realignment);
    }

    public async Task<RealignmentView> GetRealignmentAsync(Guid realignmentId, CancellationToken ct)
    {
        var realignment = await _realignments.GetByIdAsync(realignmentId, ct)
                          ?? throw new PortfolioNotFoundException($"No realignment '{realignmentId}' exists.");

        if (realignment.Status == RealignmentStatus.Pending && realignment.UpvestExecutionId is not null)
        {
            try
            {
                var outcome = await _upvest.GetRealignmentOutcomeAsync(realignment.UpvestExecutionId.Value, ct);
                if (outcome != ProviderOutcome.Pending)
                {
                    ApplyOutcome(realignment, outcome);
                    await _realignments.UpdateAsync(realignment, ct);
                }
            }
            catch (UpvestGatewayException ex)
            {
                _logger.LogWarning("Could not reconcile realignment {RealignmentId} (status {Status}).", realignment.RealignmentId, ex.StatusCode);
            }
        }

        return ToView(realignment);
    }

    private static void ApplyOutcome(PortfolioRealignment realignment, ProviderOutcome outcome)
    {
        switch (outcome)
        {
            case ProviderOutcome.Succeeded: realignment.MarkCompleted(); break;
            case ProviderOutcome.Failed: realignment.MarkFailed(); break;
            default: realignment.MarkPending(); break;
        }
    }

    // ----------------------------------------------------------------- Flow 5: portfolio

    public async Task<PortfolioView> GetPortfolioAsync(string buyerId, CancellationToken ct)
    {
        var investor = await _investors.GetByIdAsync(buyerId, ct)
                       ?? throw new PortfolioNotFoundException("You have not signed up as an investor.");

        var payments = await _payments.ListAsync(new PortfolioPaymentsByBuyerSpecification(buyerId), ct);
        // Total amount paid in = everything credited that has not failed.
        var paidIn = payments.Where(p => p.Status != PaymentStatus.Failed).Sum(p => p.Amount);

        IReadOnlyList<HoldingView> holdings = Array.Empty<HoldingView>();
        decimal totalValue = 0m;

        if (investor.IsProvisioned)
        {
            try
            {
                var snapshot = await _upvest.GetPortfolioAsync(investor.UpvestAccountId!.Value, ct);
                holdings = snapshot.Holdings.Select(h => new HoldingView(h.InstrumentId, h.Quantity, h.Value)).ToList();
                totalValue = snapshot.TotalValue;
            }
            catch (UpvestGatewayException ex)
            {
                _logger.LogWarning("Could not read portfolio for investor {InvestorId} from Upvest (status {Status}).", investor.InvestorId, ex.StatusCode);
            }
        }

        return new PortfolioView(investor.SelectedModelId, holdings, totalValue, paidIn);
    }

    // ----------------------------------------------------------------- validation & mapping

    private static void ValidateRegistration(InvestorRegistration r)
    {
        if (r is null) throw new PortfolioValidationException("A sign-up form is required.");
        RequireText(r.FirstName, "firstName");
        RequireText(r.LastName, "lastName");
        RequireText(r.Email, "email");
        RequireText(r.TaxId, "taxId");
        RequireIso2(r.Nationality, "nationality");
        RequireIso2(r.TaxCountry, "taxCountry");
        if (r.Address is null) throw new PortfolioValidationException("address is required.");
        RequireText(r.Address.Line1, "address.line1");
        RequireText(r.Address.Postcode, "address.postcode");
        RequireText(r.Address.City, "address.city");
        RequireIso2(r.Address.Country, "address.country");
        RequireDate(r.BirthDate, "birthDate");
        if (r.IdentityDocument is null) throw new PortfolioValidationException("identityDocument is required.");
        RequireText(r.IdentityDocument.Type, "identityDocument.type");
        RequireText(r.IdentityDocument.Number, "identityDocument.number");
        RequireIso2(r.IdentityDocument.IssuingCountry, "identityDocument.issuingCountry");
        RequireDate(r.IdentityDocument.ExpiryDate, "identityDocument.expiryDate");
    }

    private static void ValidateModel(string name, IReadOnlyList<ModelFund> funds)
    {
        RequireText(name, "name");
        if (funds is null || funds.Count == 0)
            throw new PortfolioValidationException("A model must list at least one fund.");

        var isins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in funds)
        {
            if (string.IsNullOrWhiteSpace(f.InstrumentId))
                throw new PortfolioValidationException("Each fund must have an instrumentId (ISIN).");
            if (!isins.Add(f.InstrumentId))
                throw new PortfolioValidationException($"Fund '{f.InstrumentId}' is listed more than once.");
            if (f.Weight <= 0m || f.Weight > 100m)
                throw new PortfolioValidationException($"Weight for '{f.InstrumentId}' must be between 0 and 100.");
            if (decimal.Round(f.Weight, 5) != f.Weight)
                throw new PortfolioValidationException($"Weight for '{f.InstrumentId}' may have at most 5 decimal places.");
        }

        var total = funds.Sum(f => f.Weight);
        if (total != 100m)
            throw new PortfolioValidationException($"Fund weights must add up to exactly 100 (they add up to {total}).");
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0m)
            throw new PortfolioValidationException("amount must be greater than 0.");
        if (amount > MaxAmount)
            throw new PortfolioValidationException($"amount must not exceed {MaxAmount}.");
        if (decimal.Round(amount, 2) != amount)
            throw new PortfolioValidationException("amount may have at most 2 decimal places.");
    }

    private static void RequireText(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new PortfolioValidationException($"{field} is required.");
    }

    private static void RequireIso2(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length != 2)
            throw new PortfolioValidationException($"{field} must be an ISO 3166-1 alpha-2 country code.");
    }

    private static void RequireDate(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _))
            throw new PortfolioValidationException($"{field} must be an ISO-8601 date.");
    }

    private static bool IsUniquenessConflict(Exception ex)
    {
        // The in-memory and SQL providers surface a duplicate key differently; match by the common
        // shapes without coupling to a provider type.
        var type = ex.GetType().Name;
        return type.Contains("DbUpdate", StringComparison.OrdinalIgnoreCase)
               || ex.Message.Contains("same key", StringComparison.OrdinalIgnoreCase)
               || ex.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }

    private static InvestorView ToView(PortfolioInvestor i) =>
        new(i.InvestorId, i.Status.ToString().ToLowerInvariant());

    private static ModelView ToView(ModelPortfolio m) =>
        new(m.ModelId, m.Name, m.Funds.Select(f => new ModelFundView(f.InstrumentId, f.Weight)).ToList());

    private static PaymentView ToView(PortfolioPayment p) =>
        new(p.PaymentId, p.Amount, p.Status.ToString().ToLowerInvariant());

    private static RealignmentView ToView(PortfolioRealignment r) =>
        new(r.RealignmentId, r.Status.ToString().ToLowerInvariant());
}
