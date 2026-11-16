using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Microsoft.eShopWeb.Infrastructure.Upvest;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio;

/// <summary>
/// Orchestrates the managed-portfolio flows across the local store and Upvest. Every reported
/// status is derived from Upvest's live state. Personal details are forwarded to Upvest for
/// sign-up only and are never persisted or logged here.
/// </summary>
public sealed class PortfolioService : IPortfolioService
{
    private const string Currency = "EUR";
    private const string AccountGroupType = "PERSONAL";
    private const string AccountType = "PORTFOLIO";

    private readonly IPortfolioStore _store;
    private readonly IUpvestClient _upvest;
    private readonly IAppLogger<PortfolioService> _logger;

    public PortfolioService(IPortfolioStore store, IUpvestClient upvest, IAppLogger<PortfolioService> logger)
    {
        _store = store;
        _upvest = upvest;
        _logger = logger;
    }

    // ---------------- Flow 1 — Becoming an investor ----------------

    public async Task<InvestorResult> SignUpInvestorAsync(string shopperId, InvestorSignUp signUp, CancellationToken ct = default)
    {
        var existing = _store.GetInvestor(shopperId);
        if (existing is not null)
        {
            _logger.LogInformation("Investor sign-up already exists for shopper; returning current status.");
            return await ComputeInvestorResultAsync(existing, ct).ConfigureAwait(false);
        }

        // Create the Upvest user. This is the one mandatory step — failure aborts the sign-up.
        var user = await _upvest.CreateUserAsync(signUp, ct).ConfigureAwait(false);
        _logger.LogInformation("Created Upvest user {UserId} (status {Status}).", user.Id, user.Status);

        var investor = new InvestorRecord { ShopperId = shopperId, UpvestUserId = user.Id };

        // Submit regulatory checks and set up the portfolio account. These require scopes beyond
        // user creation; where the environment refuses them we record what we have and continue,
        // so the sign-up is captured and reports as pending.
        await TryStep("submit KYC check", () => _upvest.SubmitKycCheckAsync(user.Id, new KycCheckInput(
            MapDocumentType(signUp.IdentityDocument.Type),
            signUp.IdentityDocument.ExpiryDate,
            signUp.Nationality,
            signUp.Address), ct)).ConfigureAwait(false);

        await TryStep("submit instrument-fit check", () => _upvest.SubmitInstrumentFitCheckAsync(user.Id, true, ct)).ConfigureAwait(false);

        if (UpvestClient.RequiresIdentifier(signUp.Nationality))
        {
            await TryStep("create identifier", () => _upvest.CreateIdentifierAsync(
                user.Id, "NATIONAL_ID", signUp.IdentityDocument.IssuingCountry, "PASSPORT", signUp.IdentityDocument.Number, ct)).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(signUp.TaxCountry))
        {
            await TryStep("set tax residencies", () => _upvest.SetTaxResidenciesAsync(user.Id,
                new[] { new TaxResidencyInput(signUp.TaxCountry!, signUp.TaxId) }, ct)).ConfigureAwait(false);
        }

        await TryStep("create account group", async () =>
        {
            var group = await _upvest.CreateAccountGroupAsync(user.Id, AccountGroupType, ct).ConfigureAwait(false);
            investor.UpvestAccountGroupId = group.Id;
            _logger.LogInformation("Created Upvest account group {GroupId} (status {Status}).", group.Id, group.Status);
        }).ConfigureAwait(false);

        if (investor.UpvestAccountGroupId is not null)
        {
            await TryStep("create account", async () =>
            {
                var account = await _upvest.CreateAccountAsync(user.Id, investor.UpvestAccountGroupId!, AccountType, ct).ConfigureAwait(false);
                investor.UpvestAccountId = account.Id;
                _logger.LogInformation("Created Upvest account {AccountId} (status {Status}).", account.Id, account.Status);
            }).ConfigureAwait(false);
        }

        _store.SaveInvestor(investor);
        return await ComputeInvestorResultAsync(investor, ct).ConfigureAwait(false);
    }

    public async Task<InvestorResult?> GetInvestorAsync(string shopperId, CancellationToken ct = default)
    {
        var investor = _store.GetInvestor(shopperId);
        if (investor is null) return null;
        return await ComputeInvestorResultAsync(investor, ct).ConfigureAwait(false);
    }

    private async Task<InvestorResult> ComputeInvestorResultAsync(InvestorRecord investor, CancellationToken ct)
        => new(investor.UpvestUserId, await ComputeInvestorStatusAsync(investor, ct).ConfigureAwait(false));

    private async Task<InvestorStatus> ComputeInvestorStatusAsync(InvestorRecord investor, CancellationToken ct)
    {
        if (investor.Rejected) return InvestorStatus.Rejected;
        if (investor.UpvestAccountId is null) return InvestorStatus.Pending;

        try
        {
            var account = await _upvest.GetAccountAsync(investor.UpvestAccountId, ct).ConfigureAwait(false);
            if (account is null) return InvestorStatus.Pending;
            return account.Status switch
            {
                "ACTIVE" => InvestorStatus.Active,
                "CLOSED" or "LOCKED" => InvestorStatus.Rejected,
                _ => InvestorStatus.Pending
            };
        }
        catch (UpvestApiException ex)
        {
            // Status cannot be read (e.g. scope not authorised in this environment): not yet known.
            _logger.LogWarning("Could not read account status from Upvest (HTTP {Status}); reporting pending.", ex.StatusCode);
            return InvestorStatus.Pending;
        }
    }

    // ---------------- Flow 2 — Model portfolios ----------------

    public async Task<ModelPortfolio> UpsertModelAsync(string modelId, string name, IReadOnlyList<ModelFund> funds, CancellationToken ct = default)
    {
        ValidateModel(name, funds);

        var upvestFunds = funds
            .Select(f => new UpvestFund(f.InstrumentId, "ISIN", (f.Weight / 100m).ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();

        var existing = _store.GetModel(modelId);
        string? allocationId = existing?.UpvestAllocationId;

        try
        {
            UpvestAllocation allocation = existing?.UpvestAllocationId is { } id
                ? await _upvest.ReplaceAllocationAsync(id, name, upvestFunds, ct).ConfigureAwait(false)
                : await _upvest.CreateAllocationAsync(name, upvestFunds, ct).ConfigureAwait(false);
            allocationId = allocation.Id;
            _logger.LogInformation("Upserted Upvest allocation {AllocationId} for model {ModelId}.", allocationId, modelId);
        }
        catch (UpvestApiException ex)
        {
            _logger.LogWarning("Could not upsert allocation at Upvest for model {ModelId} (HTTP {Status}); stored locally.", modelId, ex.StatusCode);
        }

        var model = new ModelPortfolio
        {
            ModelId = modelId,
            Name = name,
            Funds = funds,
            UpvestAllocationId = allocationId
        };
        _store.SaveModel(model);
        return model;
    }

    public Task<IReadOnlyList<ModelPortfolio>> GetModelsAsync(CancellationToken ct = default)
        => Task.FromResult(_store.GetModels());

    public async Task ChooseModelAsync(string shopperId, string modelId, CancellationToken ct = default)
    {
        var investor = _store.GetInvestor(shopperId)
            ?? throw PortfolioException.Conflict("You must sign up as an investor before choosing a model.");
        var model = _store.GetModel(modelId)
            ?? throw PortfolioException.NotFound($"Model '{modelId}' was not found.");

        investor.SelectedModelId = modelId;
        _store.SaveInvestor(investor);

        if (investor.UpvestAccountId is null || model.UpvestAllocationId is null)
        {
            _logger.LogInformation("Recorded model selection for shopper locally; Upvest configuration deferred (account or allocation not provisioned).");
            return;
        }

        try
        {
            var current = await _upvest.GetConfigurationAsync(investor.UpvestAccountId, ct).ConfigureAwait(false);
            if (current is null)
                await _upvest.CreateConfigurationAsync(investor.UpvestAccountId, model.UpvestAllocationId, ct).ConfigureAwait(false);
            else
                await _upvest.UpdateConfigurationAsync(investor.UpvestAccountId, model.UpvestAllocationId, ct).ConfigureAwait(false);
            _logger.LogInformation("Linked account {AccountId} to allocation {AllocationId} at Upvest.", investor.UpvestAccountId, model.UpvestAllocationId);
        }
        catch (UpvestApiException ex)
        {
            _logger.LogWarning("Could not update Upvest portfolio configuration (HTTP {Status}); selection stored locally.", ex.StatusCode);
        }
    }

    // ---------------- Flow 3 — Paying in ----------------

    public async Task<PaymentResult> PayInAsync(string shopperId, decimal amount, CancellationToken ct = default)
    {
        if (amount <= 0m)
            throw PortfolioException.BadRequest("Amount must be greater than zero.");
        amount = decimal.Round(amount, 2, MidpointRounding.ToEven);

        var investor = _store.GetInvestor(shopperId)
            ?? throw PortfolioException.Conflict("You must be an accepted investor with a model chosen before paying in.");

        if (string.IsNullOrEmpty(investor.SelectedModelId))
            throw PortfolioException.Conflict("You must choose a model before paying in.");

        var status = await ComputeInvestorStatusAsync(investor, ct).ConfigureAwait(false);
        if (status != InvestorStatus.Active)
            throw PortfolioException.Conflict("You must be an accepted investor whose account is ready before paying in.");

        if (investor.UpvestAccountGroupId is null || investor.UpvestAccountId is null)
            throw PortfolioException.Conflict("Your investor account is not fully provisioned yet.");

        var payment = new PaymentRecord
        {
            PaymentId = Guid.NewGuid().ToString(),
            ShopperId = shopperId,
            Amount = amount
        };

        try
        {
            // Credit the account at Upvest (sandbox virtual cash), then invest across the model.
            await _upvest.IncreaseVirtualCashAsync(investor.UpvestAccountGroupId, amount, Currency, ct).ConfigureAwait(false);
            var order = await _upvest.CreatePortfolioOrderAsync(investor.UpvestUserId, investor.UpvestAccountId, amount, Currency, ct).ConfigureAwait(false);
            payment.UpvestOrderId = order.Id;
            _logger.LogInformation("Placed portfolio order {OrderId} for a pay-in (status {Status}).", order.Id, order.Status);
        }
        catch (UpvestApiException ex)
        {
            payment.SubmissionFailed = true;
            _logger.LogWarning("Pay-in could not be placed at Upvest (HTTP {Status}).", ex.StatusCode);
        }

        _store.AddPayment(payment);
        return new PaymentResult(payment.PaymentId, payment.Amount, await ComputePaymentStatusAsync(payment, ct).ConfigureAwait(false));
    }

    public async Task<IReadOnlyList<PaymentResult>> GetPaymentsAsync(string shopperId, CancellationToken ct = default)
    {
        var payments = _store.GetPayments(shopperId); // already newest first
        var results = new List<PaymentResult>(payments.Count);
        foreach (var p in payments)
            results.Add(new PaymentResult(p.PaymentId, p.Amount, await ComputePaymentStatusAsync(p, ct).ConfigureAwait(false)));
        return results;
    }

    private async Task<PaymentStatus> ComputePaymentStatusAsync(PaymentRecord payment, CancellationToken ct)
    {
        if (payment.SubmissionFailed) return PaymentStatus.Failed;
        if (payment.UpvestOrderId is null) return PaymentStatus.Pending;
        try
        {
            var order = await _upvest.GetPortfolioOrderAsync(payment.UpvestOrderId, ct).ConfigureAwait(false);
            if (order is null) return PaymentStatus.Pending;
            return order.Status switch
            {
                "FILLED" or "SETTLED" => PaymentStatus.Invested,
                "CANCELLED" => PaymentStatus.Failed,
                _ => PaymentStatus.Pending
            };
        }
        catch (UpvestApiException ex)
        {
            _logger.LogWarning("Could not read portfolio order status from Upvest (HTTP {Status}); reporting pending.", ex.StatusCode);
            return PaymentStatus.Pending;
        }
    }

    // ---------------- Flow 4 — Realignment ----------------

    public async Task<RealignmentResult> RealignAsync(string modelId, CancellationToken ct = default)
    {
        var model = _store.GetModel(modelId)
            ?? throw PortfolioException.NotFound($"Model '{modelId}' was not found.");
        if (model.UpvestAllocationId is null)
            throw PortfolioException.Conflict("This model is not yet provisioned at Upvest, so it cannot be realigned.");

        try
        {
            string executionId = await _upvest.TriggerRealignmentAsync(model.UpvestAllocationId, ct).ConfigureAwait(false);
            _store.AddRealignment(new RealignmentRecord { RealignmentId = executionId, ModelId = modelId });
            _logger.LogInformation("Triggered realignment {ExecutionId} for model {ModelId}.", executionId, modelId);
            return new RealignmentResult(executionId, RealignmentStatus.Pending);
        }
        catch (UpvestApiException ex)
        {
            var record = new RealignmentRecord { RealignmentId = Guid.NewGuid().ToString(), ModelId = modelId, SubmissionFailed = true };
            _store.AddRealignment(record);
            _logger.LogWarning("Realignment could not be triggered at Upvest for model {ModelId} (HTTP {Status}).", modelId, ex.StatusCode);
            return new RealignmentResult(record.RealignmentId, RealignmentStatus.Failed);
        }
    }

    public async Task<RealignmentResult?> GetRealignmentAsync(string realignmentId, CancellationToken ct = default)
    {
        var record = _store.GetRealignment(realignmentId);
        if (record is null) return null;
        if (record.SubmissionFailed) return new RealignmentResult(realignmentId, RealignmentStatus.Failed);

        try
        {
            var orders = await _upvest.ListRealignmentOrdersAsync(realignmentId, ct).ConfigureAwait(false);
            return new RealignmentResult(realignmentId, DeriveRealignmentStatus(orders));
        }
        catch (UpvestApiException ex)
        {
            _logger.LogWarning("Could not read realignment status from Upvest (HTTP {Status}); reporting pending.", ex.StatusCode);
            return new RealignmentResult(realignmentId, RealignmentStatus.Pending);
        }
    }

    private static RealignmentStatus DeriveRealignmentStatus(IReadOnlyList<UpvestRebalancingOrder> orders)
    {
        if (orders.Count == 0) return RealignmentStatus.Pending;
        if (orders.Any(o => o.Status is "NEW" or "PROCESSING")) return RealignmentStatus.Pending;

        bool anyFilled = orders.Any(o => o.Status is "FILLED");
        bool anyRealFailure = orders.Any(o => o.Status == "CANCELLED" && o.CancellationReason is not null && o.CancellationReason != "PORTFOLIO_IS_BALANCED");
        if (anyRealFailure && !anyFilled) return RealignmentStatus.Failed;
        return RealignmentStatus.Completed;
    }

    // ---------------- Flow 5 — The investor's portfolio ----------------

    public async Task<PortfolioView> GetPortfolioAsync(string shopperId, CancellationToken ct = default)
    {
        var investor = _store.GetInvestor(shopperId)
            ?? throw PortfolioException.NotFound("You are not signed up as an investor.");

        decimal paidIn = _store.GetPayments(shopperId)
            .Where(p => !p.SubmissionFailed)
            .Sum(p => p.Amount);
        paidIn = decimal.Round(paidIn, 2, MidpointRounding.ToEven);

        var holdings = new List<Holding>();
        decimal totalValue = 0m;

        if (investor.UpvestAccountId is not null)
        {
            try
            {
                var valuation = await _upvest.GetCurrentValuationAsync(investor.UpvestAccountId, ct).ConfigureAwait(false);
                if (valuation is not null)
                {
                    totalValue = decimal.Round(valuation.TotalValue, 2, MidpointRounding.ToEven);
                    holdings.AddRange(valuation.Positions.Select(p =>
                        new Holding(p.InstrumentId, p.Quantity, decimal.Round(p.Value, 2, MidpointRounding.ToEven))));
                }
            }
            catch (UpvestApiException ex)
            {
                _logger.LogWarning("Could not read valuation from Upvest (HTTP {Status}); returning known contributions only.", ex.StatusCode);
            }
        }

        return new PortfolioView(investor.SelectedModelId, holdings, totalValue, paidIn);
    }

    // ---------------- Helpers ----------------

    private async Task TryStep(string description, Func<Task> step)
    {
        try
        {
            await step().ConfigureAwait(false);
        }
        catch (UpvestApiException ex)
        {
            _logger.LogWarning("Onboarding step '{Step}' did not complete at Upvest (HTTP {Status}).", description, ex.StatusCode);
        }
    }

    private static void ValidateModel(string name, IReadOnlyList<ModelFund> funds)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw PortfolioException.BadRequest("Model name is required.");
        if (funds is null || funds.Count == 0)
            throw PortfolioException.BadRequest("A model must contain at least one fund.");
        if (funds.Any(f => string.IsNullOrWhiteSpace(f.InstrumentId)))
            throw PortfolioException.BadRequest("Every fund must have an instrumentId (ISIN).");
        if (funds.Any(f => f.Weight <= 0m))
            throw PortfolioException.BadRequest("Every fund weight must be greater than zero.");
        if (funds.Select(f => f.InstrumentId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != funds.Count)
            throw PortfolioException.BadRequest("A fund may appear only once in a model.");
        decimal total = funds.Sum(f => f.Weight);
        if (total != 100m)
            throw PortfolioException.BadRequest($"Fund weights must add up to exactly 100 (got {total}).");
    }

    private static string MapDocumentType(string type) => type?.Trim().ToUpperInvariant() switch
    {
        "PASSPORT" => "PASSPORT",
        "ID_CARD" or "NATIONAL_ID" or "IDENTITY_CARD" => "ID_CARD",
        "RESIDENCE_PERMIT" => "RESIDENCE_PERMIT",
        "DRIVING_LICENSE" or "DRIVERS_LICENSE" or "DRIVING_LICENCE" => "DRIVING_LICENSE",
        _ => "PASSPORT"
    };
}
