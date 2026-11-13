using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Orchestrates the managed-portfolio flows. Each write follows claim → Upvest call → record the result,
/// so a second caller is stopped by the app store before reaching Upvest, and every status is derived
/// from a Upvest read rather than assumed. Upvest scope refusals (401/403) — expected in the sandbox —
/// are tolerated: the app record is kept and the status simply stays pending until the scope is available.
/// </summary>
public class PortfolioService : IPortfolioService
{
    // ISIN: 2 letters (country), 9 alphanumerics, 1 check digit.
    private static readonly Regex IsinPattern = new("^[A-Z]{2}[A-Z0-9]{9}[0-9]$", RegexOptions.Compiled);

    private readonly IRepository<Investor> _investors;
    private readonly IRepository<ModelPortfolio> _models;
    private readonly IRepository<Payment> _payments;
    private readonly IRepository<Realignment> _realignments;
    private readonly IUpvestPortfolioService _upvest;
    private readonly IAppLogger<PortfolioService> _logger;

    public PortfolioService(
        IRepository<Investor> investors,
        IRepository<ModelPortfolio> models,
        IRepository<Payment> payments,
        IRepository<Realignment> realignments,
        IUpvestPortfolioService upvest,
        IAppLogger<PortfolioService> logger)
    {
        _investors = investors;
        _models = models;
        _payments = payments;
        _realignments = realignments;
        _upvest = upvest;
        _logger = logger;
    }

    // ------------------------------------------------------------------ Flow 1

    public async Task<Investor> SignUpInvestorAsync(string buyerId, InvestorSignUpForm form, CancellationToken cancellationToken)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        var isNew = investor is null;

        if (investor is null)
        {
            investor = new Investor(
                buyerId,
                form.FirstName, form.LastName, form.Email, form.BirthDate,
                form.BirthCity, form.BirthCountry, form.Nationality,
                form.Address.Line1, form.Address.Postcode, form.Address.City, form.Address.Country,
                form.PhoneNumber, form.TaxId, form.TaxCountry,
                form.IdentityDocument.Type, form.IdentityDocument.Number, form.IdentityDocument.IssuingCountry, form.IdentityDocument.ExpiryDate);

            // Claim: persist the investor row before any Upvest call so a racing second sign-up is refused
            // by the store (unique BuyerId) rather than creating a duplicate Upvest user.
            await _investors.AddAsync(investor, cancellationToken);
        }
        else if (investor.UpvestUserId.HasValue && investor.UpvestAccountId.HasValue)
        {
            // Already fully onboarded — idempotent: just report the current state.
            await RefreshInvestorStatus(investor, cancellationToken);
            await _investors.UpdateAsync(investor, cancellationToken);
            return investor;
        }

        var onboarding = new InvestorOnboardingData(
            form.FirstName, form.LastName, form.Email, form.BirthDate,
            form.BirthCity, form.BirthCountry, form.Nationality,
            form.Address.Line1, form.Address.Postcode, form.Address.City, form.Address.Country,
            form.PhoneNumber, form.TaxId, form.TaxCountry);

        // Onboarding is resumable: each step is skipped once its Upvest id is recorded, and every step reuses
        // the stored idempotency key, so a resend replays the original Upvest write rather than duplicating it.
        if (!investor.UpvestUserId.HasValue)
        {
            try
            {
                var userId = await _upvest.CreateUserAsync(onboarding, investor.UserIdempotencyKey, cancellationToken);
                investor.SetUpvestUser(userId);
                _logger.LogInformation("Investor {InvestorId} created Upvest user {UserId}.", investor.Id, userId);
            }
            catch (UpvestIntegrationException ex) when (!ex.OutcomeUnknown)
            {
                // Upvest definitively refused to create the user — release a brand-new claim so the shopper can retry.
                _logger.LogWarning("Upvest refused user creation for investor sign-up (status {Status}).", ex.StatusCode);
                if (isNew) await _investors.DeleteAsync(investor, cancellationToken);
                throw;
            }
        }

        // Remaining steps are best-effort: a scope refusal (sandbox) or downstream error leaves the investor
        // pending with whatever Upvest ids were obtained — the status is re-derived on read.
        if (investor.UpvestUserId.HasValue && !investor.TaxResidenciesSet)
        {
            await TryOnboardingStep(investor, "tax residencies", async () =>
            {
                await _upvest.SetTaxResidenciesAsync(investor.UpvestUserId!.Value, form.TaxCountry, form.TaxId, investor.TaxIdempotencyKey, cancellationToken);
                investor.MarkTaxResidenciesSet();
            });
        }

        if (investor.UpvestUserId.HasValue && !investor.UpvestAccountGroupId.HasValue)
        {
            var accountGroupId = await TryOnboardingStep(investor, "account group", () =>
                _upvest.CreateAccountGroupAsync(investor.UpvestUserId!.Value, investor.AccountGroupIdempotencyKey, cancellationToken));
            if (accountGroupId.HasValue) investor.SetAccountGroup(accountGroupId.Value);
        }

        if (investor.UpvestUserId.HasValue && investor.UpvestAccountGroupId.HasValue && !investor.UpvestAccountId.HasValue)
        {
            var accountId = await TryOnboardingStep(investor, "portfolio account", () =>
                _upvest.CreateAccountAsync(investor.UpvestUserId!.Value, investor.UpvestAccountGroupId!.Value, "Managed portfolio", investor.AccountIdempotencyKey, cancellationToken));
            if (accountId.HasValue) investor.SetAccount(accountId.Value);
        }

        await RefreshInvestorStatus(investor, cancellationToken);
        await _investors.UpdateAsync(investor, cancellationToken);
        return investor;
    }

    public async Task<Investor?> GetInvestorAsync(string buyerId, CancellationToken cancellationToken)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        if (investor is null) return null;

        if (await RefreshInvestorStatus(investor, cancellationToken))
        {
            await _investors.UpdateAsync(investor, cancellationToken);
        }
        return investor;
    }

    private async Task TryOnboardingStep(Investor investor, string step, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (UpvestIntegrationException ex)
        {
            _logger.LogWarning("Onboarding step '{Step}' did not complete for investor {InvestorId} (status {Status}); investor stays pending.", step, investor.Id, ex.StatusCode);
        }
    }

    private async Task<Guid?> TryOnboardingStep(Investor investor, string step, Func<Task<Guid>> action)
    {
        try
        {
            return await action();
        }
        catch (UpvestIntegrationException ex)
        {
            _logger.LogWarning("Onboarding step '{Step}' did not complete for investor {InvestorId} (status {Status}); investor stays pending.", step, investor.Id, ex.StatusCode);
            return null;
        }
    }

    /// <summary>Re-derive the investor's status from Upvest. Returns true if the status changed.</summary>
    private async Task<bool> RefreshInvestorStatus(Investor investor, CancellationToken cancellationToken)
    {
        if (!investor.UpvestUserId.HasValue) return false;

        try
        {
            var status = await _upvest.GetInvestorStatusAsync(investor.UpvestUserId.Value, investor.UpvestAccountId, cancellationToken);
            if (status != investor.Status)
            {
                investor.UpdateStatus(status);
                return true;
            }
        }
        catch (UpvestIntegrationException ex)
        {
            // Could not read the status (e.g. scope refusal) — keep the last known value.
            _logger.LogWarning("Could not refresh investor {InvestorId} status from Upvest (status {Status}).", investor.Id, ex.StatusCode);
        }
        return false;
    }

    // ------------------------------------------------------------------ Flow 2

    public async Task<ModelPortfolio> UpsertModelAsync(string modelId, string name, IReadOnlyList<FundWeight> funds, CancellationToken cancellationToken)
    {
        ValidateFunds(funds);
        await ValidateInstrumentsAsync(funds, cancellationToken);

        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), cancellationToken);
        var isNew = model is null;
        if (model is null)
        {
            model = new ModelPortfolio(modelId, name);
        }

        model.Replace(name, funds.Select(f => new ModelFund(f.Isin, f.Weight)));

        if (isNew)
        {
            // Claim: persist the model (unique ModelId) before creating the Upvest allocation.
            await _models.AddAsync(model, cancellationToken);
        }

        try
        {
            if (model.UpvestAllocationId.HasValue)
            {
                await _upvest.UpdateModelAllocationAsync(model.UpvestAllocationId.Value, name, funds, cancellationToken);
                _logger.LogInformation("Model {ModelId} updated Upvest allocation {AllocationId}.", modelId, model.UpvestAllocationId);
            }
            else
            {
                var allocationId = await _upvest.CreateModelAllocationAsync(name, funds, model.AllocationIdempotencyKey, cancellationToken);
                model.SetUpvestAllocation(allocationId);
                _logger.LogInformation("Model {ModelId} created Upvest allocation {AllocationId}.", modelId, allocationId);
            }
        }
        catch (UpvestIntegrationException ex) when (ex.IsAuthorizationFailure)
        {
            // Sandbox lacks portfolios:admin — keep the operator's model definition; the Upvest allocation
            // is created when the scope is available. Investors can follow it only once it has an allocation.
            _logger.LogWarning("Upvest allocation write not permitted for model {ModelId} (status {Status}); model stored without a Upvest allocation.", modelId, ex.StatusCode);
        }

        await _models.UpdateAsync(model, cancellationToken);
        return model;
    }

    public async Task<IReadOnlyList<ModelPortfolio>> GetModelsAsync(CancellationToken cancellationToken)
    {
        return await _models.ListAsync(new ModelPortfoliosWithFundsSpecification(), cancellationToken);
    }

    public async Task ChooseModelAsync(string buyerId, string modelId, CancellationToken cancellationToken)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        if (investor is null)
        {
            throw new PortfolioConflictException("You must sign up as an investor before choosing a model.");
        }

        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), cancellationToken);
        if (model is null)
        {
            throw new PortfolioNotFoundException($"No model portfolio '{modelId}' exists.");
        }

        // Switching to another model is allowed; the account's Upvest configuration is updated below.
        investor.SelectModel(modelId, model.UpvestAllocationId);

        // If the investor has an Upvest portfolio account and the model has an allocation, point the account at it.
        if (investor.UpvestAccountId.HasValue && model.UpvestAllocationId.HasValue)
        {
            try
            {
                if (investor.ConfigurationCreated)
                {
                    await _upvest.UpdateInvestorConfigurationAsync(investor.UpvestAccountId.Value, model.UpvestAllocationId.Value, investor.AccountIdempotencyKey, cancellationToken);
                }
                else
                {
                    await _upvest.CreateInvestorConfigurationAsync(investor.UpvestAccountId.Value, model.UpvestAllocationId.Value, investor.AccountIdempotencyKey, cancellationToken);
                    investor.MarkConfigurationCreated();
                }
                _logger.LogInformation("Investor {InvestorId} now follows model {ModelId}.", investor.Id, modelId);
            }
            catch (UpvestIntegrationException ex) when (ex.IsAuthorizationFailure)
            {
                _logger.LogWarning("Upvest portfolio configuration not permitted for investor {InvestorId} (status {Status}); selection recorded in the app only.", investor.Id, ex.StatusCode);
            }
        }

        await _investors.UpdateAsync(investor, cancellationToken);
    }

    // ------------------------------------------------------------------ Flow 3

    public async Task<Payment> PayInAsync(string buyerId, decimal amount, CancellationToken cancellationToken)
    {
        if (amount <= 0m)
        {
            throw new PortfolioValidationException("The amount to pay in must be greater than zero.");
        }

        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        if (investor is null)
        {
            throw new PortfolioConflictException("You must sign up as an investor before paying in.");
        }

        // Status must reflect Upvest: refuse unless Upvest has accepted the investor, their account is ready, and a model is chosen.
        await RefreshInvestorStatus(investor, cancellationToken);
        await _investors.UpdateAsync(investor, cancellationToken);
        if (!investor.CanPayIn)
        {
            throw new PortfolioConflictException("You can pay in only once Upvest has accepted you, your account is ready, and you have chosen a model.");
        }

        // Claim: persist the payment (unique PaymentId) before any Upvest call.
        var payment = new Payment(Guid.NewGuid(), buyerId, investor.Id, amount);
        await _payments.AddAsync(payment, cancellationToken);

        try
        {
            var topUpId = await _upvest.CreateTopUpAsync(investor.UpvestAccountGroupId!.Value, amount, payment.PaymentId, cancellationToken);
            payment.SetTopUp(topUpId);

            var orderId = await _upvest.PlacePortfolioOrderAsync(investor.UpvestUserId!.Value, investor.UpvestAccountId!.Value, amount, payment.OrderIdempotencyKey, cancellationToken);
            payment.SetOrder(orderId);

            var status = await _upvest.GetPaymentStatusAsync(payment.UpvestTopUpId, payment.UpvestOrderId, cancellationToken);
            payment.UpdateStatus(status);
            _logger.LogInformation("Payment {PaymentId} placed (top-up {TopUpId}, order {OrderId}), status {PaymentStatus}.", payment.PaymentId, topUpId, orderId, status);
        }
        catch (UpvestIntegrationException ex)
        {
            if (ex.OutcomeUnknown || ex.IsAuthorizationFailure)
            {
                // Outcome unknown (transport) or not permitted here (sandbox scope): leave the payment pending
                // to be settled from Upvest on a later read, rather than reporting a failure that may not be one.
                _logger.LogWarning("Payment {PaymentId} left pending: Upvest write could not be confirmed (status {Status}, outcomeUnknown {Unknown}).", payment.PaymentId, ex.StatusCode, ex.OutcomeUnknown);
            }
            else
            {
                payment.UpdateStatus(PaymentStatus.Failed);
                _logger.LogWarning("Payment {PaymentId} failed at Upvest (status {Status}).", payment.PaymentId, ex.StatusCode);
            }
        }

        await _payments.UpdateAsync(payment, cancellationToken);
        return payment;
    }

    public async Task<IReadOnlyList<Payment>> GetPaymentsAsync(string buyerId, CancellationToken cancellationToken)
    {
        var payments = await _payments.ListAsync(new PaymentsByBuyerIdSpecification(buyerId), cancellationToken);

        foreach (var payment in payments.Where(p => p.Status == PaymentStatus.Pending && (p.UpvestTopUpId.HasValue || p.UpvestOrderId.HasValue)))
        {
            try
            {
                var status = await _upvest.GetPaymentStatusAsync(payment.UpvestTopUpId, payment.UpvestOrderId, cancellationToken);
                if (status != payment.Status)
                {
                    payment.UpdateStatus(status);
                    await _payments.UpdateAsync(payment, cancellationToken);
                }
            }
            catch (UpvestIntegrationException ex)
            {
                _logger.LogWarning("Could not refresh payment {PaymentId} status (status {Status}).", payment.PaymentId, ex.StatusCode);
            }
        }

        return payments;
    }

    // ------------------------------------------------------------------ Flow 4

    public async Task<Realignment> TriggerRealignmentAsync(string modelId, CancellationToken cancellationToken)
    {
        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), cancellationToken);
        if (model is null)
        {
            throw new PortfolioNotFoundException($"No model portfolio '{modelId}' exists.");
        }
        if (!model.UpvestAllocationId.HasValue)
        {
            throw new PortfolioConflictException($"Model '{modelId}' has no Upvest allocation yet, so there is nothing to realign.");
        }

        // Claim: persist the realignment (unique RealignmentId) before triggering at Upvest.
        var realignment = new Realignment(Guid.NewGuid(), modelId, model.UpvestAllocationId.Value);
        await _realignments.AddAsync(realignment, cancellationToken);

        try
        {
            var executionId = await _upvest.TriggerRealignmentAsync(model.UpvestAllocationId.Value, realignment.RealignmentId, cancellationToken);
            realignment.SetExecution(executionId);
            _logger.LogInformation("Realignment {RealignmentId} triggered Upvest execution {ExecutionId}.", realignment.RealignmentId, executionId);
        }
        catch (UpvestIntegrationException ex)
        {
            if (ex.OutcomeUnknown || ex.IsAuthorizationFailure)
            {
                _logger.LogWarning("Realignment {RealignmentId} left pending: Upvest trigger could not be confirmed (status {Status}, outcomeUnknown {Unknown}).", realignment.RealignmentId, ex.StatusCode, ex.OutcomeUnknown);
            }
            else
            {
                realignment.UpdateStatus(RealignmentStatus.Failed);
                _logger.LogWarning("Realignment {RealignmentId} failed at Upvest (status {Status}).", realignment.RealignmentId, ex.StatusCode);
            }
        }

        await _realignments.UpdateAsync(realignment, cancellationToken);
        return realignment;
    }

    public async Task<Realignment?> GetRealignmentAsync(Guid realignmentId, CancellationToken cancellationToken)
    {
        var realignment = await _realignments.FirstOrDefaultAsync(new RealignmentByIdSpecification(realignmentId), cancellationToken);
        if (realignment is null) return null;

        if (realignment.Status == RealignmentStatus.Pending && realignment.UpvestExecutionId.HasValue)
        {
            try
            {
                var status = await _upvest.GetRealignmentStatusAsync(realignment.UpvestExecutionId.Value, cancellationToken);
                if (status != realignment.Status)
                {
                    realignment.UpdateStatus(status);
                    await _realignments.UpdateAsync(realignment, cancellationToken);
                }
            }
            catch (UpvestIntegrationException ex)
            {
                _logger.LogWarning("Could not refresh realignment {RealignmentId} status (status {Status}).", realignmentId, ex.StatusCode);
            }
        }

        return realignment;
    }

    // ------------------------------------------------------------------ Flow 5

    public async Task<InvestorPortfolio> GetPortfolioAsync(string buyerId, CancellationToken cancellationToken)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        if (investor is null)
        {
            throw new PortfolioConflictException("You must sign up as an investor to have a portfolio.");
        }

        var payments = await _payments.ListAsync(new PaymentsByBuyerIdSpecification(buyerId), cancellationToken);
        // Amount paid in: money that reached or is reaching the account (everything not failed).
        var paidIn = payments.Where(p => p.Status != PaymentStatus.Failed).Sum(p => p.Amount);

        if (!investor.UpvestAccountId.HasValue)
        {
            // No Upvest account yet (e.g. onboarding not completed in this environment): app-level view.
            return new InvestorPortfolio(investor.SelectedModelId, Array.Empty<PortfolioHolding>(), 0m, paidIn);
        }

        try
        {
            var view = await _upvest.GetPortfolioAsync(investor.UpvestAccountId.Value, cancellationToken);
            var modelId = await ResolveModelIdAsync(view.AllocationId, investor, cancellationToken);
            return new InvestorPortfolio(modelId, view.Holdings, view.TotalValue, paidIn);
        }
        catch (UpvestIntegrationException ex) when (ex.IsAuthorizationFailure)
        {
            _logger.LogWarning("Could not read investor {InvestorId} portfolio from Upvest (status {Status}); returning the app-level view.", investor.Id, ex.StatusCode);
            return new InvestorPortfolio(investor.SelectedModelId, Array.Empty<PortfolioHolding>(), 0m, paidIn);
        }
    }

    private async Task<string?> ResolveModelIdAsync(Guid allocationId, Investor investor, CancellationToken cancellationToken)
    {
        if (investor.SelectedAllocationId == allocationId && investor.SelectedModelId is not null)
        {
            return investor.SelectedModelId;
        }
        var all = await _models.ListAsync(cancellationToken);
        return all.FirstOrDefault(m => m.UpvestAllocationId == allocationId)?.ModelId ?? investor.SelectedModelId;
    }

    // ------------------------------------------------------------------ Validation helpers

    private static void ValidateFunds(IReadOnlyList<FundWeight> funds)
    {
        if (funds is null || funds.Count == 0)
        {
            throw new PortfolioValidationException("A model must have at least one fund.");
        }

        foreach (var fund in funds)
        {
            if (string.IsNullOrWhiteSpace(fund.Isin) || !IsinPattern.IsMatch(fund.Isin))
            {
                throw new PortfolioValidationException($"'{fund.Isin}' is not a valid ISIN (instrumentId).");
            }
            if (fund.Weight < 0m || fund.Weight > 100m)
            {
                throw new PortfolioValidationException($"Weight {fund.Weight} for '{fund.Isin}' must be between 0 and 100.");
            }
        }

        var total = funds.Sum(f => f.Weight);
        if (total != 100m)
        {
            throw new PortfolioValidationException($"Fund weights must sum to exactly 100, but summed to {total}.");
        }

        var duplicate = funds.GroupBy(f => f.Isin).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new PortfolioValidationException($"Fund '{duplicate.Key}' appears more than once in the model.");
        }
    }

    /// <summary>
    /// Cross-operation invariant: each ISIN must be a tradeable instrument at Upvest. The check reads Upvest's
    /// instrument list; when that read is unavailable (e.g. the client lacks <c>instruments:read</c>), it degrades
    /// to the format-only validation already applied by <see cref="ValidateFunds"/>.
    /// </summary>
    private async Task ValidateInstrumentsAsync(IReadOnlyList<FundWeight> funds, CancellationToken cancellationToken)
    {
        try
        {
            var check = await _upvest.CheckInstrumentsAsync(funds.Select(f => f.Isin).Distinct().ToArray(), cancellationToken);
            if (check.Available && check.UnknownIsins.Count > 0)
            {
                throw new PortfolioValidationException($"These instrumentIds are not tradeable instruments at Upvest: {string.Join(", ", check.UnknownIsins)}.");
            }
        }
        catch (UpvestIntegrationException ex)
        {
            _logger.LogWarning("Could not validate instruments against Upvest (status {Status}); using format-only ISIN validation.", ex.StatusCode);
        }
    }
}
