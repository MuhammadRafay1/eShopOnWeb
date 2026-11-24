using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.Infrastructure.Upvest;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Services;

/// <summary>
/// Orchestrates investor onboarding, model portfolios, pay-ins, realignments and portfolio views.
/// Local persistence maps a shopper to their Upvest resources; every reported status is read live
/// from Upvest so it reflects what actually happened there. Personal details are never logged.
/// </summary>
public class ManagedPortfolioService : IManagedPortfolioService
{
    private const string Euro = "EUR";

    private readonly IRepository<Investor> _investors;
    private readonly IRepository<ModelPortfolio> _models;
    private readonly IRepository<PortfolioPayment> _payments;
    private readonly IRepository<Realignment> _realignments;
    private readonly IUpvestClient _upvest;
    private readonly UpvestSettings _settings;
    private readonly IAppLogger<ManagedPortfolioService> _logger;

    public ManagedPortfolioService(
        IRepository<Investor> investors,
        IRepository<ModelPortfolio> models,
        IRepository<PortfolioPayment> payments,
        IRepository<Realignment> realignments,
        IUpvestClient upvest,
        IOptions<UpvestSettings> settings,
        IAppLogger<ManagedPortfolioService> logger)
    {
        _investors = investors;
        _models = models;
        _payments = payments;
        _realignments = realignments;
        _upvest = upvest;
        _settings = settings.Value;
        _logger = logger;
    }

    // ---------------------------------------------------------------- Flow 1: investor

    public async Task<InvestorResult> SignUpInvestorAsync(string shopperUserName, InvestorSignUp signUp, CancellationToken ct = default)
    {
        var existing = await _investors.FirstOrDefaultAsync(new InvestorByUserNameSpec(shopperUserName), ct);
        if (existing is { UpvestUserId: not null })
        {
            _logger.LogInformation("Investor sign-up already present for shopper; returning current status.");
            return new InvestorResult(existing.UpvestUserId!, await DeriveInvestorStatusAsync(existing, ct));
        }

        var nowRfc3339 = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        var createUser = new UpvestCreateUserRequest
        {
            FirstName = signUp.FirstName,
            LastName = signUp.LastName,
            Email = string.IsNullOrWhiteSpace(signUp.Email) ? null : signUp.Email,
            BirthDate = signUp.BirthDate,
            BirthCity = signUp.BirthCity,
            BirthCountry = signUp.BirthCountry,
            Nationalities = new List<string> { signUp.Nationality },
            Address = new UpvestAddress
            {
                AddressLine1 = signUp.Address.Line1,
                Postcode = signUp.Address.Postcode,
                City = signUp.Address.City,
                Country = signUp.Address.Country,
            },
            PhoneNumber = NormalisePhoneNumber(signUp.PhoneNumber),
            Fatca = new UpvestFatca { Status = false, ConfirmedAt = nowRfc3339 },
            TermsAndConditions = string.IsNullOrWhiteSpace(_settings.TermsAndConditionsConsentId)
                ? null
                : new UpvestConsent { ConsentDocumentId = _settings.TermsAndConditionsConsentId, ConfirmedAt = nowRfc3339 },
            DataPrivacyAndSharingAgreement = string.IsNullOrWhiteSpace(_settings.DataPrivacyConsentId)
                ? null
                : new UpvestConsent { ConsentDocumentId = _settings.DataPrivacyConsentId, ConfirmedAt = nowRfc3339 },
        };

        var user = await _upvest.CreateUserAsync(createUser, ct);

        var investor = existing ?? new Investor(shopperUserName);
        investor.SetUpvestUser(user.Id);

        // Provision the account group + portfolio account. If the environment is not authorised
        // for account operations (401/403), the investor is still registered (user created) and
        // reported as pending – their account is simply not ready yet. Any other failure is real.
        try
        {
            var group = await _upvest.CreateAccountGroupAsync(new UpvestCreateAccountGroupRequest
            {
                UserId = user.Id,
                Type = "PERSONAL",
                BusinessId = _settings.BusinessId,
            }, ct);
            var account = await _upvest.CreateAccountAsync(new UpvestCreateAccountRequest
            {
                UserId = user.Id,
                AccountGroupId = group.Id,
                Type = "PORTFOLIO",
                BusinessId = _settings.BusinessId,
            }, ct);
            investor.SetUpvestAccount(group.Id, account.Id);
            _logger.LogInformation("Investor onboarded at Upvest (account created, awaiting activation).");
        }
        catch (UpvestApiException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _logger.LogWarning("Account provisioning not authorised in this environment; investor registered as pending: {0}", ex.Message);
        }

        if (existing is null)
            await _investors.AddAsync(investor, ct);
        else
            await _investors.UpdateAsync(investor, ct);

        // Best-effort enrichment: record the shop's identity-document check and tax residency.
        await TryRecordKycCheckAsync(user.Id, signUp, nowRfc3339, ct);
        await TrySetTaxResidencyAsync(user.Id, signUp, ct);

        return new InvestorResult(user.Id, await DeriveInvestorStatusAsync(investor, ct));
    }

    public async Task<InvestorResult?> GetInvestorAsync(string shopperUserName, CancellationToken ct = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByUserNameSpec(shopperUserName), ct);
        if (investor?.UpvestUserId is null)
            return null;

        return new InvestorResult(investor.UpvestUserId, await DeriveInvestorStatusAsync(investor, ct));
    }

    private async Task<InvestorStatus> DeriveInvestorStatusAsync(Investor investor, CancellationToken ct)
    {
        if (investor.UpvestAccountId is null)
            return InvestorStatus.Pending;

        try
        {
            var account = await _upvest.GetAccountAsync(investor.UpvestAccountId, ct);
            var status = account.Status?.ToUpperInvariant();
            if (status == "ACTIVE")
                return InvestorStatus.Active;
            if (status is "LOCKED" or "CLOSED" or "CLOSING")
                return InvestorStatus.Rejected;

            // Still pending approval – a failed KYC check means Upvest will not take them on.
            if (investor.UpvestUserId is not null && await AnyKycFailedAsync(investor.UpvestUserId, ct))
                return InvestorStatus.Rejected;

            return InvestorStatus.Pending;
        }
        catch (UpvestApiException ex)
        {
            // Outcome not yet knowable from Upvest -> still pending.
            _logger.LogWarning("Could not read investor status from Upvest: {0}", ex.Message);
            return InvestorStatus.Pending;
        }
    }

    private async Task<bool> AnyKycFailedAsync(string userId, CancellationToken ct)
    {
        try
        {
            var checks = await _upvest.ListChecksAsync(userId, ct);
            return checks.Any(c => string.Equals(c.Status, "FAILED", StringComparison.OrdinalIgnoreCase));
        }
        catch (UpvestApiException)
        {
            return false;
        }
    }

    private async Task TryRecordKycCheckAsync(string userId, InvestorSignUp signUp, string nowRfc3339, CancellationToken ct)
    {
        try
        {
            await _upvest.CreateKycCheckAsync(userId, new UpvestCreateKycCheckRequest
            {
                Type = "KYC",
                CheckConfirmedAt = nowRfc3339,
                DocumentType = MapDocumentType(signUp.IdentityDocument.Type),
                DocumentExpirationDate = signUp.IdentityDocument.ExpiryDate,
                Nationality = signUp.Nationality,
                Provider = "eShopOnWeb",
                Method = "ELECTRONIC_ID",
            }, ct);
        }
        catch (UpvestApiException ex)
        {
            _logger.LogWarning("Identity-document check could not be recorded at Upvest: {0}", ex.Message);
        }
    }

    private async Task TrySetTaxResidencyAsync(string userId, InvestorSignUp signUp, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(signUp.TaxCountry))
            return;
        try
        {
            await _upvest.SetTaxResidenciesAsync(userId, new UpvestTaxResidenciesRequest
            {
                TaxResidencies = new List<UpvestTaxResidency>
                {
                    new()
                    {
                        Country = signUp.TaxCountry,
                        TaxIdentifierNumber = string.IsNullOrWhiteSpace(signUp.TaxId) ? null : signUp.TaxId,
                        MissingTinReason = string.IsNullOrWhiteSpace(signUp.TaxId) ? "OTHER_REASONS" : null,
                    },
                },
            }, ct);
        }
        catch (UpvestApiException ex)
        {
            _logger.LogWarning("Tax residency could not be set at Upvest: {0}", ex.Message);
        }
    }

    private static string? NormalisePhoneNumber(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;
        // Upvest expects 8-15 digits with no '+', spaces or punctuation.
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length is >= 8 and <= 15 ? digits : null;
    }

    private static string MapDocumentType(string? type)
    {
        var normalised = (type ?? string.Empty).Trim().ToUpperInvariant().Replace(' ', '_');
        return normalised switch
        {
            "PASSPORT" => "PASSPORT",
            "ID_CARD" or "IDCARD" or "NATIONAL_ID" or "IDENTITY_CARD" => "ID_CARD",
            "RESIDENCE_PERMIT" => "RESIDENCE_PERMIT",
            "DRIVING_LICENSE" or "DRIVERS_LICENSE" or "DRIVING_LICENCE" or "DRIVER_LICENSE" => "DRIVING_LICENSE",
            _ => "ID_CARD",
        };
    }

    // ---------------------------------------------------------------- Flow 2: models

    public async Task<ModelResult> UpsertModelAsync(string modelId, string name, IReadOnlyList<FundInput> funds, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            throw new PortfolioValidationException("modelId is required.");
        if (string.IsNullOrWhiteSpace(name))
            throw new PortfolioValidationException("name is required.");
        if (funds is null || funds.Count == 0)
            throw new PortfolioValidationException("At least one fund is required.");

        List<ModelFund> modelFunds;
        try
        {
            modelFunds = funds.Select(f => new ModelFund(f.InstrumentId, f.Weight)).ToList();
        }
        catch (ArgumentException ex)
        {
            throw new PortfolioValidationException(ex.Message);
        }

        var totalWeight = modelFunds.Sum(f => f.Weight);
        if (totalWeight != 100m)
            throw new PortfolioValidationException($"Fund weights must sum to exactly 100 but summed to {totalWeight}.");

        var request = new UpvestAllocationRequest
        {
            Name = name,
            Allocation = modelFunds.Select(f => new UpvestAllocationEntry
            {
                InstrumentId = f.InstrumentId,
                InstrumentIdType = "ISIN",
                Weight = f.Weight.ToString(CultureInfo.InvariantCulture),
            }).ToList(),
        };

        var existing = await _models.FirstOrDefaultAsync(new ModelByModelIdSpec(modelId), ct);

        if (existing?.UpvestAllocationId is not null)
        {
            await _upvest.ReplaceAllocationAsync(existing.UpvestAllocationId, request, ct);
            existing.Replace(name, modelFunds);
            await _models.UpdateAsync(existing, ct);
            return ToModelResult(existing);
        }

        var allocation = await _upvest.CreateAllocationAsync(request, ct);

        if (existing is null)
        {
            var model = new ModelPortfolio(modelId, name, modelFunds);
            model.SetAllocationId(allocation.Id);
            await _models.AddAsync(model, ct);
            return ToModelResult(model);
        }

        existing.Replace(name, modelFunds);
        existing.SetAllocationId(allocation.Id);
        await _models.UpdateAsync(existing, ct);
        return ToModelResult(existing);
    }

    public async Task<IReadOnlyList<ModelResult>> GetModelsAsync(CancellationToken ct = default)
    {
        var models = await _models.ListAsync(new AllModelsSpec(), ct);
        return models.Select(ToModelResult).ToList();
    }

    public async Task SelectModelAsync(string shopperUserName, string modelId, CancellationToken ct = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByUserNameSpec(shopperUserName), ct);
        if (investor is null)
            throw new PortfolioConflictException("You must sign up as an investor before choosing a model.");
        if (investor.UpvestAccountId is null)
            throw new PortfolioConflictException("Your investor account is not ready yet; try again once it is active.");

        var model = await _models.FirstOrDefaultAsync(new ModelByModelIdSpec(modelId), ct);
        if (model?.UpvestAllocationId is null)
            throw new PortfolioNotFoundException($"Model '{modelId}' does not exist.");

        var configuration = new UpvestConfigurationRequest
        {
            AccountId = investor.UpvestAccountId,
            AllocationId = model.UpvestAllocationId,
        };

        try
        {
            await _upvest.CreateConfigurationAsync(configuration, ct);
        }
        catch (UpvestApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            // Already has a configuration – switch it to the newly chosen model.
            await _upvest.UpdateConfigurationAsync(investor.UpvestAccountId, configuration, ct);
        }

        investor.SelectModel(modelId);
        await _investors.UpdateAsync(investor, ct);
    }

    // ---------------------------------------------------------------- Flow 3: pay-ins

    public async Task<PaymentResult> PayInAsync(string shopperUserName, decimal amount, CancellationToken ct = default)
    {
        if (amount <= 0m)
            throw new PortfolioValidationException("amount must be greater than zero.");
        if (decimal.Round(amount, 2) != amount)
            throw new PortfolioValidationException("amount must have at most two decimal places.");

        var investor = await _investors.FirstOrDefaultAsync(new InvestorByUserNameSpec(shopperUserName), ct);
        if (investor is null)
            throw new PortfolioConflictException("You must sign up as an investor before paying in.");
        if (investor.UpvestAccountId is null || investor.UpvestAccountGroupId is null)
            throw new PortfolioConflictException("Your investor account is not ready yet; you cannot pay in.");
        if (investor.SelectedModelId is null)
            throw new PortfolioConflictException("You must choose a model before paying in.");

        var status = await DeriveInvestorStatusAsync(investor, ct);
        if (status != InvestorStatus.Active)
            throw new PortfolioConflictException("Your investor account is not active yet; you cannot pay in.");

        var amountText = amount.ToString("0.00", CultureInfo.InvariantCulture);

        // Sandbox funding: credit the account group's cash, then invest it across the model.
        var cashIncrease = await _upvest.IncreaseVirtualCashAsync(new UpvestVirtualCashIncreaseRequest
        {
            AccountGroupId = investor.UpvestAccountGroupId,
            Amount = amountText,
            Currency = Euro,
        }, ct);

        var order = await _upvest.CreatePortfolioOrderAsync(new UpvestPortfolioOrderRequest
        {
            UserId = investor.UpvestUserId!,
            AccountId = investor.UpvestAccountId,
            CashAmount = amountText,
            Currency = Euro,
            Side = "BUY",
        }, ct);

        var payment = new PortfolioPayment(shopperUserName, amount, Euro);
        payment.SetUpvestReferences(cashIncrease.Id, order.Id);
        await _payments.AddAsync(payment, ct);

        _logger.LogInformation("Pay-in placed at Upvest as portfolio order.");
        return new PaymentResult(payment.PublicId, payment.Amount, MapPaymentStatus(order.Status));
    }

    public async Task<IReadOnlyList<PaymentResult>> GetPaymentsAsync(string shopperUserName, CancellationToken ct = default)
    {
        var payments = await _payments.ListAsync(new PaymentsByUserNameSpec(shopperUserName), ct);
        var results = new List<PaymentResult>(payments.Count);
        foreach (var payment in payments)
            results.Add(new PaymentResult(payment.PublicId, payment.Amount, await DerivePaymentStatusAsync(payment, ct)));
        return results;
    }

    private async Task<PaymentStatus> DerivePaymentStatusAsync(PortfolioPayment payment, CancellationToken ct)
    {
        if (payment.UpvestOrderId is null)
            return PaymentStatus.Failed;
        try
        {
            var order = await _upvest.GetPortfolioOrderAsync(payment.UpvestOrderId, ct);
            return MapPaymentStatus(order.Status);
        }
        catch (UpvestApiException ex)
        {
            _logger.LogWarning("Could not read payment status from Upvest: {0}", ex.Message);
            return PaymentStatus.Pending;
        }
    }

    private static PaymentStatus MapPaymentStatus(string? upvestStatus) =>
        upvestStatus?.ToUpperInvariant() switch
        {
            "FILLED" or "SETTLED" => PaymentStatus.Invested,
            "CANCELLED" => PaymentStatus.Failed,
            _ => PaymentStatus.Pending,
        };

    // ---------------------------------------------------------------- Flow 4: realignment

    public async Task<RealignmentResult> TriggerRealignmentAsync(string modelId, CancellationToken ct = default)
    {
        var model = await _models.FirstOrDefaultAsync(new ModelByModelIdSpec(modelId), ct);
        if (model?.UpvestAllocationId is null)
            throw new PortfolioNotFoundException($"Model '{modelId}' does not exist.");

        var execution = await _upvest.TriggerRebalancingAsync(new UpvestRebalancingRequest
        {
            Allocations = new List<string> { model.UpvestAllocationId },
        }, ct);

        var realignment = new Realignment(modelId);
        realignment.SetExecutionId(execution.Id);
        await _realignments.AddAsync(realignment, ct);

        _logger.LogInformation("Realignment triggered at Upvest for model allocation.");
        return new RealignmentResult(realignment.PublicId, await DeriveRealignmentStatusAsync(realignment, ct));
    }

    public async Task<RealignmentResult> GetRealignmentAsync(Guid realignmentId, CancellationToken ct = default)
    {
        var realignment = await _realignments.FirstOrDefaultAsync(new RealignmentByPublicIdSpec(realignmentId), ct);
        if (realignment is null)
            throw new PortfolioNotFoundException($"Realignment '{realignmentId}' does not exist.");

        return new RealignmentResult(realignment.PublicId, await DeriveRealignmentStatusAsync(realignment, ct));
    }

    private async Task<RealignmentStatus> DeriveRealignmentStatusAsync(Realignment realignment, CancellationToken ct)
    {
        if (realignment.UpvestExecutionId is null)
            return RealignmentStatus.Pending;
        try
        {
            var orders = await _upvest.GetRebalancingOrdersAsync(realignment.UpvestExecutionId, ct);
            if (orders.Count == 0)
                return RealignmentStatus.Pending;

            var statuses = orders.Select(o => o.Status?.ToUpperInvariant()).ToList();
            if (statuses.Any(s => s is "NEW" or "PROCESSING" or null))
                return RealignmentStatus.Pending;
            if (statuses.All(s => s == "FILLED"))
                return RealignmentStatus.Completed;
            return RealignmentStatus.Failed;
        }
        catch (UpvestApiException ex)
        {
            _logger.LogWarning("Could not read realignment status from Upvest: {0}", ex.Message);
            return RealignmentStatus.Pending;
        }
    }

    // ---------------------------------------------------------------- Flow 5: portfolio

    public async Task<PortfolioResult?> GetPortfolioAsync(string shopperUserName, CancellationToken ct = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByUserNameSpec(shopperUserName), ct);
        if (investor is null)
            return null;

        var paidIn = (await _payments.ListAsync(new PaymentsByUserNameSpec(shopperUserName), ct)).Sum(p => p.Amount);

        var holdings = new List<HoldingResult>();
        decimal totalValue = 0m;
        try
        {
            if (investor.UpvestAccountId is null)
                return new PortfolioResult(investor.SelectedModelId, holdings, 0m, decimal.Round(paidIn, 2));

            var valuation = await _upvest.GetCurrentValuationAsync(investor.UpvestAccountId, ct);
            totalValue = ParseDecimal(valuation.TotalSecurityValue?.Amount);
            foreach (var position in valuation.SecurityPositions)
            {
                var instrumentId = position.Instrument?.Isin ?? position.Instrument?.Uuid ?? string.Empty;
                holdings.Add(new HoldingResult(
                    instrumentId,
                    ParseDecimal(position.Quantity),
                    ParseDecimal(position.Value?.Amount)));
            }
        }
        catch (UpvestApiException ex)
        {
            // Valuation not readable yet – report what we know (model + amount paid in).
            _logger.LogWarning("Could not read portfolio valuation from Upvest: {0}", ex.Message);
        }

        return new PortfolioResult(investor.SelectedModelId, holdings, decimal.Round(totalValue, 2), decimal.Round(paidIn, 2));
    }

    // ---------------------------------------------------------------- helpers

    private static ModelResult ToModelResult(ModelPortfolio model) =>
        new(model.ModelId, model.Name, model.Funds.Select(f => new FundResult(f.InstrumentId, f.Weight)).ToList());

    private static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0m;
}
