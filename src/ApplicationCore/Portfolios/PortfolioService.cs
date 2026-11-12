using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolios;

/// <summary>
/// Default orchestration of the managed-portfolio flows. It coordinates the local stores with Upvest,
/// and derives every reported status from Upvest's own read endpoints so the status always reflects
/// what actually happened at Upvest. Upvest calls that the current sandbox credentials cannot make
/// (anything beyond user operations is refused) are tolerated where the flow allows it: the resulting
/// status simply stays <c>pending</c>, which is the honest outcome.
/// </summary>
public class PortfolioService : IPortfolioService
{
    // Nationalities for which Upvest derives a CONCAT identifier automatically — no identifier call needed.
    private static readonly HashSet<string> ConcatNationalities =
        new(StringComparer.OrdinalIgnoreCase) { "AT", "DE", "FR", "HU", "IE", "LU" };

    private const string Eur = "EUR";

    private readonly IUpvestInvestmentClient _upvest;
    private readonly IInvestorStore _investors;
    private readonly IModelPortfolioStore _models;
    private readonly IPaymentStore _payments;
    private readonly IRealignmentStore _realignments;

    public PortfolioService(
        IUpvestInvestmentClient upvest,
        IInvestorStore investors,
        IModelPortfolioStore models,
        IPaymentStore payments,
        IRealignmentStore realignments)
    {
        _upvest = upvest;
        _investors = investors;
        _models = models;
        _payments = payments;
        _realignments = realignments;
    }

    // ---------------------------------------------------------------- Flow 1: becoming an investor

    public async Task<Investor> SignUpInvestorAsync(string shopperId, InvestorSignUp form, CancellationToken ct = default)
    {
        var existing = await _investors.GetByShopperAsync(shopperId, ct);
        if (existing is not null)
        {
            throw new PortfolioConflictException("This shopper is already registered as an investor.");
        }

        ValidateSignUp(form);

        var investor = new Investor { ShopperId = shopperId, Status = InvestorStatus.Pending };

        // 1) Create the user at Upvest. This is the anchor of the sign-up — if it fails, the sign-up fails.
        var createUser = new CreateUserRequest(
            form.FirstName, form.LastName, form.Email, form.BirthDate,
            form.BirthCity, form.BirthCountry, form.Nationality, form.Address, form.PhoneNumber);
        var user = await _upvest.CreateUserAsync(createUser, ct);
        investor.UpvestUserId = user.Id;
        investor.Status = MapUserStatus(user.Status, accountActive: false);

        // 2) Submit the KYC check the shop performed. Async at Upvest; tolerated if the sandbox refuses it.
        await BestEffortAsync(investor, "kyc", ct2 =>
            _upvest.SubmitKycCheckAsync(user.Id, form.IdentityDocument, form.Nationality, ct2), ct);

        // 3) Declare tax residency.
        await BestEffortAsync(investor, "tax", ct2 =>
            _upvest.SetTaxResidencyAsync(user.Id, form.TaxCountry, form.TaxId, ct2), ct);

        // 4) Create a regulatory identifier, unless the nationality yields a CONCAT identifier automatically.
        if (!ConcatNationalities.Contains(form.Nationality))
        {
            await BestEffortAsync(investor, "identifier", ct2 =>
                _upvest.CreatePassportIdentifierAsync(user.Id, form.IdentityDocument.IssuingCountry, form.IdentityDocument.Number, ct2), ct);
        }

        // 5) Create the account group, then 6) the portfolio account that will hold the managed portfolio.
        try
        {
            var group = await _upvest.CreateAccountGroupAsync(user.Id, ct);
            investor.UpvestAccountGroupId = group.Id;

            var account = await _upvest.CreatePortfolioAccountAsync(user.Id, group.Id, "Managed Portfolio", ct);
            investor.UpvestAccountId = account.Id;
            investor.Status = MapUserStatus(user.Status, AccountIsActive(account.Status));
        }
        catch (UpvestApiException ex)
        {
            investor.StatusDetail = $"Account setup pending at Upvest (HTTP {ex.StatusCode}).";
        }

        return await _investors.AddAsync(investor, ct);
    }

    public async Task<Investor?> GetInvestorAsync(string shopperId, CancellationToken ct = default)
    {
        var investor = await _investors.GetByShopperAsync(shopperId, ct);
        if (investor is null) return null;
        await RefreshInvestorStatusAsync(investor, ct);
        return investor;
    }

    private async Task RefreshInvestorStatusAsync(Investor investor, CancellationToken ct)
    {
        if (investor.UpvestUserId is null) return;

        string? userStatus = null;
        try
        {
            var user = await _upvest.GetUserAsync(investor.UpvestUserId, ct);
            userStatus = user.Status;
        }
        catch (UpvestApiException ex)
        {
            investor.StatusDetail = $"Could not read investor status from Upvest (HTTP {ex.StatusCode}).";
            await _investors.UpdateAsync(investor, ct);
            return;
        }

        bool accountActive = false;
        if (investor.UpvestAccountId is not null)
        {
            try
            {
                var account = await _upvest.GetAccountAsync(investor.UpvestAccountId, ct);
                accountActive = AccountIsActive(account.Status);
            }
            catch (UpvestApiException)
            {
                accountActive = false;
            }
        }

        investor.Status = MapUserStatus(userStatus, accountActive);
        await _investors.UpdateAsync(investor, ct);
    }

    // ---------------------------------------------------------------- Flow 2: model portfolios

    public async Task<ModelPortfolio> UpsertModelAsync(string modelId, string name, IReadOnlyList<ModelFundInput> funds, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new PortfolioValidationException("A model name is required.");
        if (funds is null || funds.Count == 0)
            throw new PortfolioValidationException("A model must contain at least one fund.");

        var totalWeight = funds.Sum(f => f.Weight);
        if (totalWeight != 100m)
            throw new PortfolioValidationException($"Fund weights must add up to exactly 100 (got {totalWeight.ToString(CultureInfo.InvariantCulture)}).");
        if (funds.Any(f => f.Weight <= 0m))
            throw new PortfolioValidationException("Every fund weight must be greater than 0.");
        if (funds.Any(f => string.IsNullOrWhiteSpace(f.InstrumentId)))
            throw new PortfolioValidationException("Every fund must carry an instrumentId (ISIN).");
        if (funds.Select(f => f.InstrumentId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != funds.Count)
            throw new PortfolioValidationException("A model must not list the same instrument twice.");

        var model = await _models.GetAsync(modelId, ct) ?? new ModelPortfolio { ModelId = modelId, CreatedAt = DateTimeOffset.UtcNow };
        model.Name = name;
        model.Funds = funds.Select(f => new ModelFund { InstrumentId = f.InstrumentId, Weight = f.Weight }).ToList();
        model.UpdatedAt = DateTimeOffset.UtcNow;

        // Upvest allocations carry weights as fractions that sum to 1.0.
        var allocationFunds = funds.Select(f => new UpvestAllocationFund(f.InstrumentId, f.Weight / 100m)).ToList();

        try
        {
            if (model.UpvestAllocationId is null)
            {
                var allocation = await _upvest.CreateAllocationAsync(name, allocationFunds, ct);
                model.UpvestAllocationId = allocation.Id;
            }
            else
            {
                await _upvest.ReplaceAllocationAsync(model.UpvestAllocationId, name, allocationFunds, ct);
            }
            model.SyncDetail = "Synced with Upvest allocation.";
        }
        catch (UpvestApiException ex)
        {
            model.SyncDetail = $"Model saved; Upvest allocation sync pending (HTTP {ex.StatusCode}).";
        }

        await _models.UpsertAsync(model, ct);
        return model;
    }

    public Task<IReadOnlyList<ModelPortfolio>> ListModelsAsync(CancellationToken ct = default) => _models.ListAsync(ct);

    public async Task<Investor> ChooseModelAsync(string shopperId, string modelId, CancellationToken ct = default)
    {
        var investor = await _investors.GetByShopperAsync(shopperId, ct)
            ?? throw new NotAnInvestorException("Sign up as an investor before choosing a model.");

        var model = await _models.GetAsync(modelId, ct)
            ?? throw new PortfolioValidationException($"No model portfolio '{modelId}' exists.");

        investor.ModelId = modelId;

        // Link the investor's account to the allocation, when both are known at Upvest.
        if (investor.UpvestAccountId is not null && model.UpvestAllocationId is not null)
        {
            try
            {
                await _upvest.SetPortfolioConfigurationAsync(investor.UpvestAccountId, model.UpvestAllocationId, ct);
                investor.StatusDetail = "Model linked at Upvest.";
            }
            catch (UpvestApiException ex)
            {
                investor.StatusDetail = $"Model chosen; Upvest linking pending (HTTP {ex.StatusCode}).";
            }
        }

        await _investors.UpdateAsync(investor, ct);
        return investor;
    }

    // ---------------------------------------------------------------- Flow 3: paying in

    public async Task<Payment> PayInAsync(string shopperId, decimal amount, CancellationToken ct = default)
    {
        if (amount <= 0m)
            throw new PortfolioValidationException("The amount must be greater than 0.");
        if (decimal.Round(amount, 2) != amount)
            throw new PortfolioValidationException("The amount must have at most two decimal places.");

        var investor = await _investors.GetByShopperAsync(shopperId, ct)
            ?? throw new NotAnInvestorException("Sign up as an investor before paying in.");

        // A shopper can pay in only once Upvest has accepted them and their account is ready.
        await RefreshInvestorStatusAsync(investor, ct);
        if (investor.Status != InvestorStatus.Active)
            throw new PortfolioConflictException("You can pay in only once Upvest has accepted you as an investor and your account is ready.");
        if (string.IsNullOrEmpty(investor.ModelId))
            throw new PortfolioConflictException("Choose a model portfolio before paying in.");
        if (investor.UpvestAccountGroupId is null || investor.UpvestAccountId is null || investor.UpvestUserId is null)
            throw new PortfolioConflictException("Your Upvest account is not ready to receive a pay-in yet.");

        var payment = new Payment
        {
            ShopperId = shopperId,
            Amount = amount,
            Currency = Eur,
            Status = PaymentStatus.Pending
        };

        // Credit cash to the account, then invest it across the model's funds.
        var topup = await _upvest.CreateTopupAsync(investor.UpvestAccountGroupId, amount, Eur, ct);
        payment.UpvestTopupId = topup.Id;

        var order = await _upvest.CreateBuyOrderAsync(investor.UpvestUserId, investor.UpvestAccountId, amount, Eur, ct);
        payment.UpvestPortfolioOrderId = order.Id;
        payment.Status = MapOrderStatus(order.Status);

        return await _payments.AddAsync(payment, ct);
    }

    public async Task<IReadOnlyList<Payment>> ListPaymentsAsync(string shopperId, CancellationToken ct = default)
    {
        var payments = await _payments.ListByShopperAsync(shopperId, ct);
        foreach (var payment in payments)
        {
            if (payment.Status == PaymentStatus.Pending && payment.UpvestPortfolioOrderId is not null)
            {
                try
                {
                    var order = await _upvest.GetPortfolioOrderAsync(payment.UpvestPortfolioOrderId, ct);
                    var mapped = MapOrderStatus(order.Status);
                    if (mapped != payment.Status)
                    {
                        payment.Status = mapped;
                        await _payments.UpdateAsync(payment, ct);
                    }
                }
                catch (UpvestApiException)
                {
                    // Leave as pending; the outcome at Upvest is not yet knowable with these credentials.
                }
            }
        }
        return payments;
    }

    // ---------------------------------------------------------------- Flow 4: realignment

    public async Task<Realignment> CreateRealignmentAsync(string modelId, CancellationToken ct = default)
    {
        var model = await _models.GetAsync(modelId, ct)
            ?? throw new PortfolioValidationException($"No model portfolio '{modelId}' exists.");

        var realignment = new Realignment { ModelId = modelId, Status = RealignmentStatus.Pending };

        if (model.UpvestAllocationId is null)
        {
            realignment.StatusDetail = "Model is not yet linked to an Upvest allocation; realignment cannot run.";
            return await _realignments.AddAsync(realignment, ct);
        }

        try
        {
            var execution = await _upvest.CreateRebalancingForAllocationAsync(model.UpvestAllocationId, ct);
            realignment.UpvestExecutionId = execution.Id;
        }
        catch (UpvestApiException ex)
        {
            realignment.StatusDetail = $"Realignment could not be started at Upvest (HTTP {ex.StatusCode}).";
        }

        return await _realignments.AddAsync(realignment, ct);
    }

    public async Task<Realignment?> GetRealignmentAsync(Guid realignmentId, CancellationToken ct = default)
    {
        var realignment = await _realignments.GetAsync(realignmentId, ct);
        if (realignment is null) return null;

        if (realignment.Status == RealignmentStatus.Pending && realignment.UpvestExecutionId is not null)
        {
            try
            {
                var statuses = await _upvest.GetRebalancingOrderStatusesAsync(realignment.UpvestExecutionId, ct);
                var mapped = MapRealignmentStatus(statuses);
                if (mapped != realignment.Status)
                {
                    realignment.Status = mapped;
                    await _realignments.UpdateAsync(realignment, ct);
                }
            }
            catch (UpvestApiException)
            {
                // Outcome not yet knowable; stays pending.
            }
        }

        return realignment;
    }

    // ---------------------------------------------------------------- Flow 5: the investor's portfolio

    public async Task<PortfolioView?> GetPortfolioAsync(string shopperId, CancellationToken ct = default)
    {
        var investor = await _investors.GetByShopperAsync(shopperId, ct);
        if (investor is null) return null;

        var paidIn = (await _payments.ListByShopperAsync(shopperId, ct))
            .Where(p => p.Status != PaymentStatus.Failed)
            .Sum(p => p.Amount);

        IReadOnlyList<UpvestHolding> holdings = Array.Empty<UpvestHolding>();
        decimal totalValue = 0m;

        if (investor.UpvestAccountId is not null)
        {
            try
            {
                var valuation = await _upvest.GetCurrentValuationAsync(investor.UpvestAccountId, ct);
                holdings = valuation.Holdings;
                totalValue = valuation.TotalValue;
            }
            catch (UpvestApiException)
            {
                // No valuation available yet (e.g. nothing invested, or not readable with these credentials).
            }
        }

        return new PortfolioView(investor.ModelId, holdings, totalValue, paidIn);
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// Runs an onboarding sub-step that is allowed to fail softly (e.g. an async check, or a call the
    /// sandbox credentials cannot make). A failure is recorded as a note, never thrown — the investor's
    /// status is derived from Upvest reads regardless.
    /// </summary>
    private async Task BestEffortAsync(Investor investor, string label, Func<CancellationToken, Task> action, CancellationToken ct)
    {
        try
        {
            await action(ct);
        }
        catch (UpvestApiException ex)
        {
            investor.StatusDetail = $"{label} step pending at Upvest (HTTP {ex.StatusCode}).";
        }
    }

    private static void ValidateSignUp(InvestorSignUp form)
    {
        void Require(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new PortfolioValidationException($"'{field}' is required.");
        }

        Require(form.FirstName, "firstName");
        Require(form.LastName, "lastName");
        Require(form.Email, "email");
        Require(form.Nationality, "nationality");
        Require(form.TaxId, "taxId");
        Require(form.TaxCountry, "taxCountry");
        if (form.Address is null) throw new PortfolioValidationException("'address' is required.");
        Require(form.Address.AddressLine1, "address.line1");
        Require(form.Address.Postcode, "address.postcode");
        Require(form.Address.City, "address.city");
        Require(form.Address.Country, "address.country");
        if (form.IdentityDocument is null) throw new PortfolioValidationException("'identityDocument' is required.");
        Require(form.IdentityDocument.Type, "identityDocument.type");
        Require(form.IdentityDocument.Number, "identityDocument.number");
        Require(form.IdentityDocument.IssuingCountry, "identityDocument.issuingCountry");
        if (form.Nationality.Length != 2)
            throw new PortfolioValidationException("'nationality' must be an ISO 3166-1 alpha-2 code.");
    }

    private static bool AccountIsActive(string status) =>
        string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase);

    private static InvestorStatus MapUserStatus(string? userStatus, bool accountActive)
    {
        if (userStatus is null) return InvestorStatus.Pending;

        // OFFBOARDING / OFFBOARDED means Upvest has turned the user away.
        if (userStatus.StartsWith("OFFBOARD", StringComparison.OrdinalIgnoreCase))
            return InvestorStatus.Rejected;

        if (string.Equals(userStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase) && accountActive)
            return InvestorStatus.Active;

        return InvestorStatus.Pending;
    }

    private static PaymentStatus MapOrderStatus(string orderStatus) => orderStatus?.ToUpperInvariant() switch
    {
        "FILLED" or "SETTLED" => PaymentStatus.Invested,
        "CANCELLED" => PaymentStatus.Failed,
        _ => PaymentStatus.Pending
    };

    private static RealignmentStatus MapRealignmentStatus(IReadOnlyList<string> orderStatuses)
    {
        if (orderStatuses is null || orderStatuses.Count == 0)
            return RealignmentStatus.Pending;

        var upper = orderStatuses.Select(s => s?.ToUpperInvariant() ?? string.Empty).ToList();

        // Still work to do.
        if (upper.Any(s => s is "NEW" or "PROCESSING"))
            return RealignmentStatus.Pending;

        // Everything settled. Completed if at least one order filled; otherwise it failed.
        if (upper.Any(s => s == "FILLED"))
            return RealignmentStatus.Completed;

        return RealignmentStatus.Failed;
    }
}
