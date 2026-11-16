using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Logging;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Exceptions;
using UpvestInvestmentApi.Models;
using UpvestInvestmentApi.Models.AnyOf;
using UpvestInvestmentApi.Models.Enums;
using ReqUsers = UpvestInvestmentApi.Requests.Users;
using ReqChecks = UpvestInvestmentApi.Requests.UserChecks;
using ReqIdent = UpvestInvestmentApi.Requests.UserIdentifiers;
using ReqTax = UpvestInvestmentApi.Requests.TaxResidencies;
using ReqGroups = UpvestInvestmentApi.Requests.AccountGroups;
using ReqAccounts = UpvestInvestmentApi.Requests.AccountsApi;
using ReqPortfolios = UpvestInvestmentApi.Requests.Portfolios;
using ReqRebal = UpvestInvestmentApi.Requests.PortfoliosRebalancing;
using ReqVCash = UpvestInvestmentApi.Requests.VirtualCashBalances;
using ReqVal = UpvestInvestmentApi.Requests.Valuations;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio;

/// <summary>
/// Upvest-backed implementation of the managed-portfolio capability. Owns both the shop-side
/// mapping records and every Upvest call. Idempotency keys for writes are derived deterministically
/// from ids the shop already holds, so a retried write replays rather than acting twice. Every
/// status returned is read back from Upvest (never assumed from the local send).
/// </summary>
public sealed class UpvestManagedPortfolioService : IManagedPortfolioService
{
    private const int MaxPages = 100;
    private const int PageSize = 100;

    private readonly UpvestInvestmentApiClient _client;
    private readonly IRepository<Investor> _investors;
    private readonly IRepository<ModelPortfolio> _models;
    private readonly IRepository<Payment> _payments;
    private readonly IRepository<Realignment> _realignments;
    private readonly UpvestSettings _settings;
    private readonly ILogger<UpvestManagedPortfolioService> _logger;

    public UpvestManagedPortfolioService(
        UpvestInvestmentApiClient client,
        IRepository<Investor> investors,
        IRepository<ModelPortfolio> models,
        IRepository<Payment> payments,
        IRepository<Realignment> realignments,
        UpvestSettings settings,
        ILogger<UpvestManagedPortfolioService> logger)
    {
        _client = client;
        _investors = investors;
        _models = models;
        _payments = payments;
        _realignments = realignments;
        _settings = settings;
        _logger = logger;
    }

    // ───────────────────────────── Flow 1 — becoming an investor ─────────────────────────────

    public async Task<InvestorView> SignUpInvestorAsync(string shopUserId, InvestorSignUp signUp, CancellationToken cancellationToken = default)
    {
        var existing = await _investors.FirstOrDefaultAsync(new InvestorByShopUserIdSpec(shopUserId), cancellationToken);
        if (existing is not null)
        {
            // Idempotent re-signup: the claim (PK on ShopUserId) already exists — return current state.
            return await BuildInvestorViewAsync(existing, cancellationToken);
        }

        // 1. Create the Upvest user (mandatory — a failure here means there is no investor to record).
        var createdUser = await CallAsync("CreateUser", ct => _client.Users.CreateUser(new ReqUsers.CreateUserRequest
        {
            IdempotencyKey = DeterministicGuid.Create($"{shopUserId}:user"),
            Body = UserCreateRequest.UserTolCreateRequest(BuildUserTol(signUp))
        }, cancellationToken: ct), cancellationToken);

        if (!TryReadUser(createdUser.TryGetUserTol, createdUser.TryGetUserByol, createdUser.TryGetUnknown, out var userId, out _))
        {
            throw new UpvestProviderException("Upvest returned an unexpected user payload.");
        }

        var investor = new Investor(shopUserId);
        investor.SetUpvestUser(userId);

        // Persist the claim now (PK on ShopUserId rejects a concurrent second sign-up).
        try
        {
            await _investors.AddAsync(investor, cancellationToken);
        }
        catch (Exception ex) when (IsDuplicateKey(ex))
        {
            var winner = await _investors.FirstOrDefaultAsync(new InvestorByShopUserIdSpec(shopUserId), cancellationToken);
            if (winner is not null)
            {
                return await BuildInvestorViewAsync(winner, cancellationToken);
            }
            throw;
        }

        // 2–6. Best-effort provisioning chain. Each step reflects a real Upvest scope; where the
        // environment refuses one (sandbox is authorised for user operations only), the investor
        // stays pending and the outcome is read back from Upvest on GET.
        await TryStepAsync("CreateUserCheck", ct => _client.UserChecks.CreateUserCheck(new ReqChecks.CreateUserCheckRequest
        {
            UserId = userId,
            Body = UserCheckCreateRequest.UserCheckKnowYourCustomerCreateRequest(BuildKycCheck(signUp))
        }, cancellationToken: ct));

        await TryStepAsync("CreateIdentifier", ct => _client.UserIdentifiers.CreateIdentifier(new ReqIdent.CreateIdentifierRequest
        {
            UserId = userId,
            Body = new IdentifierCreateRequest
            {
                Type = Type7.NationalId,
                IssuingCountry = ResolveIssuingCountry(signUp.Nationality),
                Identifier = signUp.IdentityDocument.Number
            }
        }, cancellationToken: ct));

        await TryStepAsync("SetTaxResidencies", ct => _client.TaxResidencies.SetTaxResidencies(new ReqTax.SetTaxResidenciesRequest
        {
            UserId = userId,
            IdempotencyKey = DeterministicGuid.Create($"{shopUserId}:tax"),
            Body = new TaxResidenciesSetRequest
            {
                TaxResidencies = new List<TaxResidencyForCreateRequest>
                {
                    TaxResidencyForCreateRequest.WithTaxIdentifierNumber(new WithTaxIdentifierNumber
                    {
                        Country = ResolveCountry(signUp.TaxCountry),
                        TaxIdentifierNumber = signUp.TaxId
                    })
                }
            }
        }, cancellationToken: ct));

        var groupResult = await TryStepAsync("CreateAccountGroup", ct => _client.AccountGroups.CreateAccountGroup(new ReqGroups.CreateAccountGroupRequest
        {
            IdempotencyKey = DeterministicGuid.Create($"{shopUserId}:group"),
            Body = AccountGroupCreateRequest.AccountGroupCreateUserRequest(new AccountGroupCreateUserRequest
            {
                UserId = userId,
                Type = Type13.Personal
            })
        }, cancellationToken: ct));

        Guid? accountGroupId = null;
        if (groupResult is not null && groupResult.TryGetAccountGroup(out var group))
        {
            accountGroupId = group.Id;
            investor.SetAccountGroup(group.Id);
        }

        if (accountGroupId.HasValue)
        {
            var accountResult = await TryStepAsync("CreateAccount", ct => _client.AccountsApi.CreateAccount(new ReqAccounts.CreateAccountRequest
            {
                IdempotencyKey = DeterministicGuid.Create($"{shopUserId}:account"),
                Body = AccountCreateRequest.AccountCreateUserRequest(new AccountCreateUserRequest
                {
                    UserId = userId,
                    AccountGroupId = accountGroupId.Value,
                    Type = Type16.Portfolio, // the account able to hold a managed portfolio
                    Name = "Managed portfolio"
                })
            }, cancellationToken: ct));

            if (accountResult is not null && accountResult.TryGetAccount(out var account))
            {
                investor.SetAccount(account.Id);
            }
        }

        await _investors.UpdateAsync(investor, cancellationToken);
        return await BuildInvestorViewAsync(investor, cancellationToken);
    }

    public async Task<InvestorView?> GetInvestorAsync(string shopUserId, CancellationToken cancellationToken = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopUserIdSpec(shopUserId), cancellationToken);
        if (investor is null)
        {
            return null;
        }
        return await BuildInvestorViewAsync(investor, cancellationToken);
    }

    private async Task<InvestorView> BuildInvestorViewAsync(Investor investor, CancellationToken ct)
    {
        var investorId = investor.UpvestUserId ?? Guid.Empty;
        var status = await ReadInvestorStatusAsync(investor, ct);
        return new InvestorView(investorId, status);
    }

    private async Task<InvestorStatus> ReadInvestorStatusAsync(Investor investor, CancellationToken ct)
    {
        if (!investor.UpvestUserId.HasValue)
        {
            return InvestorStatus.Pending;
        }

        Status? userStatus = null;
        try
        {
            var userResponse = await CallAsync("RetrieveUser", c => _client.Users.RetrieveUser(
                new ReqUsers.RetrieveUserRequest { UserId = investor.UpvestUserId.Value }, cancellationToken: c), ct);
            if (TryReadUser(userResponse.TryGetUserTol, userResponse.TryGetUserByol, userResponse.TryGetUnknown, out _, out var resolvedStatus))
            {
                userStatus = resolvedStatus;
            }
        }
        catch (UpvestProviderException ex)
        {
            // Cannot read status right now — report pending rather than inventing a terminal state.
            _logger.LogWarning("Could not read investor status from Upvest (status {Status}).", ex.StatusCode);
            return InvestorStatus.Pending;
        }

        if (userStatus is not null && (userStatus == Status.Offboarded || userStatus == Status.Offboarding))
        {
            return InvestorStatus.Rejected;
        }

        Status21? accountStatus = null;
        if (investor.UpvestAccountId.HasValue)
        {
            try
            {
                var accountResponse = await CallAsync("RetrieveAccount", c => _client.AccountsApi.RetrieveAccount(
                    new ReqAccounts.RetrieveAccountRequest { AccountId = investor.UpvestAccountId.Value }, cancellationToken: c), ct);
                if (accountResponse.TryGetAccount(out var account))
                {
                    accountStatus = account.Status;
                }
            }
            catch (UpvestProviderException)
            {
                // Account not readable yet — remain pending.
            }
        }

        if (userStatus == Status.Active && accountStatus == Status21.Active)
        {
            return InvestorStatus.Active;
        }

        return InvestorStatus.Pending;
    }

    // ───────────────────────────── Flow 2 — model portfolios ─────────────────────────────

    public async Task<ModelPortfolioView> UpsertModelAsync(string modelId, string name, IReadOnlyList<FundWeight> funds, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new PortfolioValidationException("A model portfolio must have a name.");
        }
        if (funds is null || funds.Count == 0)
        {
            throw new PortfolioValidationException("A model portfolio must list at least one fund.");
        }
        if (funds.Any(f => f.Weight < 0m || f.Weight > 100m))
        {
            throw new PortfolioValidationException("Each fund weight must be a percentage between 0 and 100.");
        }
        var totalWeight = funds.Sum(f => f.Weight);
        if (totalWeight != 100m)
        {
            throw new PortfolioValidationException($"The weights of a model portfolio must total exactly 100, but totalled {totalWeight}.");
        }

        var fundEntities = funds.Select(f => new ModelFund(f.InstrumentId, f.Weight)).ToList();

        // Validate the ISINs are instruments Upvest offers (best-effort: skip only if the environment
        // refuses the read, which we cannot treat as "invalid").
        await ValidateInstrumentsAsync(funds.Select(f => f.InstrumentId).ToList(), cancellationToken);

        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpec(modelId), cancellationToken);
        var isNew = model is null;
        if (model is null)
        {
            model = new ModelPortfolio(modelId, name, fundEntities);
        }
        else
        {
            model.Replace(name, fundEntities);
        }

        // Sync to an Upvest allocation (best-effort; the shop model is authoritative for GET models).
        var allocation = BuildAllocation(funds);
        if (model.UpvestAllocationId is null)
        {
            var created = await TryStepAsync("CreatePortfoliosAllocation", ct => _client.Portfolios.CreatePortfoliosAllocation(new ReqPortfolios.CreatePortfoliosAllocationRequest
            {
                IdempotencyKey = DeterministicGuid.Create($"model:{modelId}:allocation"),
                Body = new PortfoliosAllocationCreateRequest { Name = name, Allocation = allocation }
            }, cancellationToken: ct));
            if (created is not null)
            {
                model.SetAllocationId(created.Id);
            }
        }
        else
        {
            await TryStepAsync("UpdatePortfoliosAllocation", ct => _client.Portfolios.UpdatePortfoliosAllocation(new ReqPortfolios.UpdatePortfoliosAllocationRequest
            {
                AllocationId = model.UpvestAllocationId.Value,
                Body = new PortfoliosAllocationUpdateRequest { Name = name, Allocation = allocation }
            }, cancellationToken: ct));
        }

        if (isNew)
        {
            await _models.AddAsync(model, cancellationToken);
        }
        else
        {
            await _models.UpdateAsync(model, cancellationToken);
        }

        return ToModelView(model);
    }

    public async Task<IReadOnlyList<ModelPortfolioView>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        // The shop's model store is authoritative for the model id / name / funds the operator defined.
        var models = await _models.ListAsync(new AllModelPortfoliosSpec(), cancellationToken);
        return models.Select(ToModelView).ToList();
    }

    public async Task SelectModelAsync(string shopUserId, string modelId, CancellationToken cancellationToken = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopUserIdSpec(shopUserId), cancellationToken)
            ?? throw new InvestorNotFoundException();

        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpec(modelId), cancellationToken)
            ?? throw new ModelPortfolioNotFoundException(modelId);

        var alreadyChosen = investor.SelectedModelId is not null;
        investor.SelectModel(modelId);

        // Link the investor's account to the allocation at Upvest (best-effort). Create on first
        // choice, update on a later switch.
        if (investor.UpvestAccountId.HasValue && model.UpvestAllocationId.HasValue)
        {
            if (!alreadyChosen)
            {
                await TryStepAsync("CreatePortfoliosConfiguration", ct => _client.Portfolios.CreatePortfoliosConfiguration(new ReqPortfolios.CreatePortfoliosConfigurationRequest
                {
                    IdempotencyKey = DeterministicGuid.Create($"{shopUserId}:configuration"),
                    Body = new PortfoliosConfigurationCreateRequest
                    {
                        AccountId = investor.UpvestAccountId.Value,
                        AllocationId = model.UpvestAllocationId.Value
                    }
                }, cancellationToken: ct));
            }
            else
            {
                await TryStepAsync("UpdatePortfoliosConfiguration", ct => _client.Portfolios.UpdatePortfoliosConfiguration(new ReqPortfolios.UpdatePortfoliosConfigurationRequest
                {
                    AccountId = investor.UpvestAccountId.Value,
                    IdempotencyKey = DeterministicGuid.Create($"{shopUserId}:configuration:{modelId}"),
                    Body = new PortfoliosConfigurationUpdateRequest { AllocationId = model.UpvestAllocationId.Value }
                }, cancellationToken: ct));
            }
        }

        await _investors.UpdateAsync(investor, cancellationToken);
    }

    // ───────────────────────────── Flow 3 — paying in ─────────────────────────────

    public async Task<PaymentView> PayInAsync(string shopUserId, decimal amount, CancellationToken cancellationToken = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopUserIdSpec(shopUserId), cancellationToken)
            ?? throw new InvestorNotFoundException();

        if (investor.SelectedModelId is null)
        {
            throw new NotAnAcceptedInvestorException("Choose a model portfolio before paying in.");
        }
        if (!investor.UpvestAccountId.HasValue || !investor.UpvestUserId.HasValue)
        {
            throw new NotAnAcceptedInvestorException("Your investor account is not ready to accept pay-ins yet.");
        }

        var status = await ReadInvestorStatusAsync(investor, cancellationToken);
        if (status != InvestorStatus.Active)
        {
            throw new NotAnAcceptedInvestorException("You must be an accepted investor before paying in.");
        }

        // Claim: create the Payment first (PK on PaymentId). The amount is recorded before any send.
        var payment = new Payment(Guid.NewGuid(), shopUserId, amount);
        await _payments.AddAsync(payment, cancellationToken);

        var accountGroupId = investor.UpvestAccountGroupId;
        var amountString = FormatMoney(amount);

        // Credit the account group's cash (sandbox funding).
        if (accountGroupId.HasValue)
        {
            var fund = await TryMoneyStepAsync("CreateVirtualCashIncrease", ct => _client.VirtualCashBalances.CreateVirtualCashIncrease(new ReqVCash.CreateVirtualCashIncreaseRequest
            {
                IdempotencyKey = DeterministicGuid.Create($"payment:{payment.PaymentId}:fund"),
                Body = new VirtualCashBalanceVirtualCashIncreaseCreateRequest
                {
                    AccountGroupId = accountGroupId.Value,
                    Amount = amountString,
                    Currency = Currency1.Eur
                }
            }, cancellationToken: ct));

            if (fund.Answered && !fund.Success)
            {
                payment.MarkFailed();
                await _payments.UpdateAsync(payment, cancellationToken);
                return new PaymentView(payment.PaymentId, amount, PaymentStatus.Failed);
            }
        }

        // Invest across the model's funds in their weights (BUY by cash amount; Upvest distributes it
        // across the account's configured allocation).
        var order = await TryOrderStepAsync(payment, investor, amountString, cancellationToken);
        if (order.Answered && !order.Success)
        {
            payment.MarkFailed();
            await _payments.UpdateAsync(payment, cancellationToken);
            return new PaymentView(payment.PaymentId, amount, PaymentStatus.Failed);
        }

        if (order.OrderId.HasValue)
        {
            payment.SetOrder(order.OrderId.Value);
        }
        await _payments.UpdateAsync(payment, cancellationToken);

        var paymentStatus = order.OrderStatus is not null
            ? MapPaymentStatus(order.OrderStatus)
            : PaymentStatus.Pending; // unknown outcome: settled on the next GET
        return new PaymentView(payment.PaymentId, amount, paymentStatus);
    }

    public async Task<IReadOnlyList<PaymentView>> ListPaymentsAsync(string shopUserId, CancellationToken cancellationToken = default)
    {
        var payments = await _payments.ListAsync(new PaymentsByShopUserIdSpec(shopUserId), cancellationToken);
        var views = new List<PaymentView>(payments.Count);
        foreach (var payment in payments)
        {
            views.Add(new PaymentView(payment.PaymentId, payment.Amount, await ReadPaymentStatusAsync(payment, cancellationToken)));
        }
        return views;
    }

    private async Task<PaymentStatus> ReadPaymentStatusAsync(Payment payment, CancellationToken ct)
    {
        if (payment.UpvestOrderId.HasValue)
        {
            try
            {
                var order = await CallAsync("RetrievePortfoliosOrder", c => _client.Portfolios.RetrievePortfoliosOrder(
                    new ReqPortfolios.RetrievePortfoliosOrderRequest { PortfolioOrderId = payment.UpvestOrderId.Value }, cancellationToken: c), ct);
                return MapPaymentStatus(order.Status);
            }
            catch (UpvestProviderException)
            {
                return PaymentStatus.Pending;
            }
        }

        return payment.LocalOutcome == PaymentLocalOutcome.Failed ? PaymentStatus.Failed : PaymentStatus.Pending;
    }

    // ───────────────────────────── Flow 4 — realignment ─────────────────────────────

    public async Task<RealignmentView> TriggerRealignmentAsync(string modelId, CancellationToken cancellationToken = default)
    {
        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpec(modelId), cancellationToken)
            ?? throw new ModelPortfolioNotFoundException(modelId);

        var realignment = new Realignment(Guid.NewGuid(), modelId);
        await _realignments.AddAsync(realignment, cancellationToken);

        if (model.UpvestAllocationId.HasValue)
        {
            var result = await TryRebalancingStepAsync(realignment, model.UpvestAllocationId.Value, cancellationToken);
            if (result.Answered && !result.Success)
            {
                realignment.MarkFailed();
                await _realignments.UpdateAsync(realignment, cancellationToken);
                return new RealignmentView(realignment.RealignmentId, RealignmentStatus.Failed);
            }
            if (result.ExecutionId.HasValue)
            {
                realignment.SetExecution(result.ExecutionId.Value);
                await _realignments.UpdateAsync(realignment, cancellationToken);
            }
        }

        return await BuildRealignmentViewAsync(realignment, cancellationToken);
    }

    public async Task<RealignmentView?> GetRealignmentAsync(Guid realignmentId, CancellationToken cancellationToken = default)
    {
        var realignment = await _realignments.FirstOrDefaultAsync(new RealignmentByIdSpec(realignmentId), cancellationToken);
        if (realignment is null)
        {
            return null;
        }
        return await BuildRealignmentViewAsync(realignment, cancellationToken);
    }

    private async Task<RealignmentView> BuildRealignmentViewAsync(Realignment realignment, CancellationToken ct)
    {
        if (!realignment.UpvestExecutionId.HasValue)
        {
            var local = realignment.LocalOutcome == RealignmentLocalOutcome.Failed
                ? RealignmentStatus.Failed
                : RealignmentStatus.Pending;
            return new RealignmentView(realignment.RealignmentId, local);
        }

        try
        {
            var status = await DeriveRealignmentStatusAsync(realignment.UpvestExecutionId.Value, ct);
            return new RealignmentView(realignment.RealignmentId, status);
        }
        catch (UpvestProviderException)
        {
            return new RealignmentView(realignment.RealignmentId, RealignmentStatus.Pending);
        }
    }

    private async Task<RealignmentStatus> DeriveRealignmentStatusAsync(Guid executionId, CancellationToken ct)
    {
        // The execution object carries no status; derive it from its orders' statuses.
        var statuses = new List<Status71>();
        var offset = 0;
        var cut = false;
        for (var page = 0; page < MaxPages; page++)
        {
            var response = await CallAsync("ListPortfolioRebalancingExecutionOrders", c => _client.PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders(
                new ReqRebal.ListPortfolioRebalancingExecutionOrdersRequest { ExecutionId = executionId, Offset = offset, Limit = PageSize }, cancellationToken: c), ct);

            statuses.AddRange(response.Data.Select(o => o.Status));
            offset += response.Data.Count;
            if (response.Data.Count < PageSize || offset >= response.Meta.TotalCount)
            {
                break;
            }
            if (page == MaxPages - 1)
            {
                cut = true;
            }
        }

        if (cut || statuses.Count == 0)
        {
            // Could not see every order — do not report a false terminal state.
            return RealignmentStatus.Pending;
        }
        if (statuses.Any(s => s == Status71.Cancelled))
        {
            return RealignmentStatus.Failed;
        }
        if (statuses.All(s => s == Status71.Filled))
        {
            return RealignmentStatus.Completed;
        }
        return RealignmentStatus.Pending;
    }

    // ───────────────────────────── Flow 5 — the investor's portfolio ─────────────────────────────

    public async Task<PortfolioView> GetPortfolioAsync(string shopUserId, CancellationToken cancellationToken = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopUserIdSpec(shopUserId), cancellationToken)
            ?? throw new InvestorNotFoundException();

        if (!investor.UpvestAccountId.HasValue)
        {
            throw new NotAnAcceptedInvestorException("Your investor account is not ready yet.");
        }

        var valuation = await CallAsync("GetAccountValuation", c => _client.Valuations.GetAccountValuation(new ReqVal.GetAccountValuationRequest
        {
            AccountId = investor.UpvestAccountId.Value,
            PriceQuality = PriceQuality3.HighestAvailable
        }, cancellationToken: c), cancellationToken);

        var holdings = valuation.SecurityPositions.Select(p => new Holding(
            InstrumentId: p.Instrument.Isin ?? p.Instrument.Uuid.ToString(),
            Quantity: ParseDecimal(p.Quantity),
            Value: p.Value is not null ? ParseDecimal(p.Value.Amount) : 0m)).ToList();

        var totalValue = ParseDecimal(valuation.TotalSecurityValue.Amount);
        var paidIn = await ComputePaidInAsync(shopUserId, cancellationToken);

        return new PortfolioView(investor.SelectedModelId, holdings, totalValue, paidIn);
    }

    private async Task<decimal> ComputePaidInAsync(string shopUserId, CancellationToken ct)
    {
        // The shop's own running total of pay-ins that were not rejected before investing.
        var payments = await _payments.ListAsync(new PaymentsByShopUserIdSpec(shopUserId), ct);
        return payments.Where(p => p.LocalOutcome != PaymentLocalOutcome.Failed).Sum(p => p.Amount);
    }

    // ───────────────────────────── Building Upvest request payloads ─────────────────────────────

    private UserTolCreateRequest BuildUserTol(InvestorSignUp s) => new()
    {
        FirstName = s.FirstName,
        LastName = s.LastName,
        Email = s.Email,
        BirthDate = ToDateTimeOffset(s.BirthDate),
        BirthCity = s.BirthCity,
        BirthCountry = ResolveBirthCountry(s.BirthCountry),
        Nationalities = new List<Nationality> { ResolveNationality(s.Nationality) },
        PhoneNumber = string.IsNullOrWhiteSpace(s.PhoneNumber) ? null : s.PhoneNumber,
        Address = new Address2
        {
            AddressLine1 = s.Address.Line1,
            Postcode = s.Address.Postcode,
            City = s.Address.City,
            Country = ResolveCountry(s.Address.Country)
        },
        Fatca = new Fatca { Status = false, ConfirmedAt = DateTimeOffset.UtcNow }
    };

    private UserCheckKnowYourCustomerCreateRequest BuildKycCheck(InvestorSignUp s) => new()
    {
        CheckConfirmedAt = DateTimeOffset.UtcNow,
        DataDownloadLink = "https://eshoponweb.example/kyc/" + Uri.EscapeDataString(s.Email),
        DocumentType = ResolveDocumentType(s.IdentityDocument.Type),
        DocumentExpirationDate = ToDateTimeOffset(s.IdentityDocument.ExpiryDate),
        Nationality = s.Nationality,
        Provider = "eShopOnWeb",
        Method = Method.ElectronicId
    };

    private static IReadOnlyList<Allocation> BuildAllocation(IReadOnlyList<FundWeight> funds) =>
        funds.Select(f => new Allocation
        {
            InstrumentId = InstrumentId.String(f.InstrumentId),
            InstrumentIdType = InstrumentIdType4.Isin,
            Weight = FormatWeight(f.Weight)
        }).ToList();

    private async Task ValidateInstrumentsAsync(IReadOnlyList<string> isins, CancellationToken ct)
    {
        var offered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var offset = 0;
            for (var page = 0; page < MaxPages; page++)
            {
                var response = await CallAsync("ListInstruments", c => _client.Instruments.ListInstruments(
                    new UpvestInvestmentApi.Requests.Instruments.ListInstrumentsRequest { Offset = offset, Limit = PageSize }, cancellationToken: c), ct);
                foreach (var instrument in response.Data)
                {
                    if (!string.IsNullOrEmpty(instrument.Isin))
                    {
                        offered.Add(instrument.Isin);
                    }
                }
                offset += response.Data.Count;
                if (response.Data.Count < PageSize || offset >= response.Meta.TotalCount)
                {
                    break;
                }
            }
        }
        catch (UpvestProviderException ex)
        {
            // Cannot read the instrument universe (e.g. scope not authorised) — cannot treat an ISIN as
            // invalid on evidence we do not have. Upvest validates on the write regardless.
            _logger.LogWarning("Skipping instrument validation; Upvest instrument list unavailable (status {Status}).", ex.StatusCode);
            return;
        }

        var unknown = isins.Where(i => !offered.Contains(i)).ToList();
        if (unknown.Count > 0)
        {
            throw new PortfolioValidationException($"These fund ISINs are not offered by Upvest: {string.Join(", ", unknown)}.");
        }
    }

    // ───────────────────────────── Upvest call helpers (error boundary) ─────────────────────────────

    /// <summary>
    /// Runs one Upvest call under the whole-call budget and translates SDK failures into a caller-safe
    /// <see cref="UpvestProviderException"/> carrying the HTTP status (null for transport/timeout). Never
    /// logs request bodies or personal data.
    /// </summary>
    private async Task<T> CallAsync<T>(string operation, Func<CancellationToken, Task<T>> call, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_settings.CallBudgetSeconds));
        try
        {
            return await call(cts.Token);
        }
        catch (ApiException ex)
        {
            // Covers ApiException<TError> (any T) and ResponseDeserializationException — status is on all.
            _logger.LogWarning("Upvest {Operation} answered {Status} for {Method} {Uri}.", operation, (int)ex.StatusCode, ex.Method, ex.RequestUri);
            throw new UpvestProviderException($"Upvest rejected the {operation} request.", ex.StatusCode, ex);
        }
        catch (AuthSchemeException ex)
        {
            _logger.LogError(ex, "Upvest {Operation} failed: credentials missing or rejected.", operation);
            throw new UpvestProviderException("Upvest credentials are missing or were rejected.", null, ex);
        }
        catch (SdkException ex)
        {
            // Connection / timeout — no usable response.
            _logger.LogWarning(ex, "Upvest {Operation} could not be completed (no response).", operation);
            throw new UpvestProviderException($"Upvest could not complete the {operation} request.", null, ex);
        }
    }

    private async Task TryStepAsync(string operation, Func<CancellationToken, Task> call)
    {
        try
        {
            await CallAsync(operation, async ct => { await call(ct); return true; }, CancellationToken.None);
        }
        catch (UpvestProviderException ex)
        {
            _logger.LogWarning("Provisioning step {Operation} did not complete (status {Status}); continuing.", operation, ex.StatusCode);
        }
    }

    private async Task<TResult?> TryStepAsync<TResult>(string operation, Func<CancellationToken, Task<TResult>> call)
        where TResult : class
    {
        try
        {
            return await CallAsync(operation, call, CancellationToken.None);
        }
        catch (UpvestProviderException ex)
        {
            _logger.LogWarning("Provisioning step {Operation} did not complete (status {Status}); continuing.", operation, ex.StatusCode);
            return null;
        }
    }

    private async Task<MoneyStepResult> TryMoneyStepAsync(string operation, Func<CancellationToken, Task<VirtualCashBalanceVirtualCashIncrease>> call)
    {
        try
        {
            await CallAsync(operation, call, CancellationToken.None);
            return new MoneyStepResult(Answered: true, Success: true);
        }
        catch (UpvestProviderException ex) when (ex.StatusCode is not null)
        {
            _logger.LogWarning("{Operation} was refused by Upvest (status {Status}).", operation, ex.StatusCode);
            return new MoneyStepResult(Answered: true, Success: false);
        }
        catch (UpvestProviderException)
        {
            // Unknown outcome (transport). The money may have moved — leave the payment pending.
            return new MoneyStepResult(Answered: false, Success: false);
        }
    }

    private async Task<OrderStepResult> TryOrderStepAsync(Payment payment, Investor investor, string amountString, CancellationToken ct)
    {
        try
        {
            var order = await CallAsync("CreatePortfoliosOrder", c => _client.Portfolios.CreatePortfoliosOrder(new ReqPortfolios.CreatePortfoliosOrderRequest
            {
                IdempotencyKey = DeterministicGuid.Create($"payment:{payment.PaymentId}:order"),
                Body = new PortfoliosOrderPlaceRequest
                {
                    UserId = investor.UpvestUserId!.Value,
                    AccountId = investor.UpvestAccountId!.Value,
                    CashAmount = amountString,
                    Currency = Currency.Eur,
                    Side = Side15.Buy
                }
            }, cancellationToken: c), ct);
            return new OrderStepResult(Answered: true, Success: true, OrderId: order.Id, OrderStatus: order.Status);
        }
        catch (UpvestProviderException ex) when (ex.StatusCode is not null)
        {
            _logger.LogWarning("CreatePortfoliosOrder was refused by Upvest (status {Status}).", ex.StatusCode);
            return new OrderStepResult(Answered: true, Success: false, OrderId: null, OrderStatus: null);
        }
        catch (UpvestProviderException)
        {
            return new OrderStepResult(Answered: false, Success: false, OrderId: null, OrderStatus: null);
        }
    }

    private async Task<RebalancingStepResult> TryRebalancingStepAsync(Realignment realignment, Guid allocationId, CancellationToken ct)
    {
        try
        {
            var response = await CallAsync("TriggerPortfolioRebalancing", c => _client.PortfoliosRebalancing.TriggerPortfolioRebalancing(new ReqRebal.TriggerPortfolioRebalancingOperationRequest
            {
                IdempotencyKey = DeterministicGuid.Create($"realignment:{realignment.RealignmentId}"),
                Body = TriggerPortfolioRebalancingRequest.Allocations(new Allocations { AllocationsValue = new List<Guid> { allocationId } })
            }, cancellationToken: c), ct);
            return new RebalancingStepResult(Answered: true, Success: true, ExecutionId: response.Id);
        }
        catch (UpvestProviderException ex) when (ex.StatusCode is not null)
        {
            _logger.LogWarning("TriggerPortfolioRebalancing was refused by Upvest (status {Status}).", ex.StatusCode);
            return new RebalancingStepResult(Answered: true, Success: false, ExecutionId: null);
        }
        catch (UpvestProviderException)
        {
            return new RebalancingStepResult(Answered: false, Success: false, ExecutionId: null);
        }
    }

    private readonly record struct MoneyStepResult(bool Answered, bool Success);
    private readonly record struct OrderStepResult(bool Answered, bool Success, Guid? OrderId, Status65? OrderStatus);
    private readonly record struct RebalancingStepResult(bool Answered, bool Success, Guid? ExecutionId);

    // ───────────────────────────── Mapping & formatting ─────────────────────────────

    private static ModelPortfolioView ToModelView(ModelPortfolio model) => new(
        model.ModelId,
        model.Name,
        model.Funds.Select(f => new FundWeight(f.InstrumentId, f.Weight)).ToList());

    private delegate bool TryArm<T>(out T? value) where T : class;
    private delegate bool TryUnknownArm(out JsonElement value);

    /// <summary>
    /// Reads the user id and status from a user response regardless of which model arm (TOL or BYOL)
    /// the AnyOf matched, falling back to the raw JSON if it matched neither.
    /// </summary>
    private static bool TryReadUser(TryArm<UserTol> tryTol, TryArm<UserByol> tryByol, TryUnknownArm tryUnknown, out Guid id, out Status status)
    {
        if (tryTol(out var tol)) { id = tol.Id; status = tol.Status; return true; }
        if (tryByol(out var byol)) { id = byol.Id; status = byol.Status; return true; }
        if (tryUnknown(out var raw) && raw.ValueKind == JsonValueKind.Object
            && raw.TryGetProperty("id", out var idEl) && idEl.TryGetGuid(out id))
        {
            status = raw.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.String
                && Status.TryGetKnownValue(st.GetString()!, out var known)
                ? known
                : Status.Inactive;
            return true;
        }
        id = Guid.Empty;
        status = Status.Inactive;
        return false;
    }

    private static PaymentStatus MapPaymentStatus(Status65 status)
    {
        if (status == Status65.Filled || status == Status65.Settled) return PaymentStatus.Invested;
        if (status == Status65.Cancelled) return PaymentStatus.Failed;
        return PaymentStatus.Pending; // New / Processing / any undeclared value
    }

    private static Country ResolveCountry(string code) => Resolve<Country>(code, Country.TryGetKnownValue, "country");
    private static Nationality ResolveNationality(string code) => Resolve<Nationality>(code, Nationality.TryGetKnownValue, "nationality");
    private static BirthCountry ResolveBirthCountry(string code) => Resolve<BirthCountry>(code, BirthCountry.TryGetKnownValue, "birth country");
    private static IssuingCountry ResolveIssuingCountry(string code) => Resolve<IssuingCountry>(code, IssuingCountry.TryGetKnownValue, "issuing country");
    private static DocumentType3 ResolveDocumentType(string type) => Resolve<DocumentType3>(NormalizeDocumentType(type), DocumentType3.TryGetKnownValue, "identity document type");

    private delegate bool TryResolve<T>(string value, out T result);

    private static T Resolve<T>(string value, TryResolve<T> tryResolve, string label)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (tryResolve(normalized, out var resolved))
        {
            return resolved;
        }
        throw new PortfolioValidationException($"'{value}' is not a recognised {label}.");
    }

    private static string NormalizeDocumentType(string type)
    {
        var normalized = (type ?? string.Empty).Trim().ToUpperInvariant().Replace(' ', '_');
        return normalized switch
        {
            "ID" or "IDENTITY_CARD" or "IDENTITYCARD" or "NATIONAL_ID" => "ID_CARD",
            "DRIVERS_LICENSE" or "DRIVER_LICENSE" or "DRIVING_LICENCE" => "DRIVING_LICENSE",
            _ => normalized
        };
    }

    private static DateTimeOffset ToDateTimeOffset(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static string FormatMoney(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatWeight(decimal weight) => weight.ToString("0.##", CultureInfo.InvariantCulture);

    private static decimal ParseDecimal(string value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0m;

    private static bool IsDuplicateKey(Exception ex) =>
        ex is Microsoft.EntityFrameworkCore.DbUpdateException
        || ex is ArgumentException
        || ex is InvalidOperationException { Message: not null } && ex.Message.Contains("same key", StringComparison.OrdinalIgnoreCase);
}
