using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Exceptions;
using UpvestInvestmentApi.Models;
using UpvestInvestmentApi.Models.AnyOf;
using UpvestInvestmentApi.Models.Enums;
using UpvestInvestmentApi.Requests.AccountGroups;
using UpvestInvestmentApi.Requests.AccountsApi;
using UpvestInvestmentApi.Requests.Portfolios;
using UpvestInvestmentApi.Requests.PortfoliosRebalancing;
using UpvestInvestmentApi.Requests.TaxResidencies;
using UpvestInvestmentApi.Requests.Tests;
using UpvestInvestmentApi.Requests.TopUps;
using UpvestInvestmentApi.Requests.UserChecks;
using UpvestInvestmentApi.Requests.Users;
using UpvestInvestmentApi.Requests.Valuations;

namespace Microsoft.eShopWeb.PublicApi.Portfolio;

/// <summary>
/// The sole place that talks to the Upvest SDK. Every call is wrapped so that a provider error,
/// transport failure, or unreadable body becomes a single <see cref="UpvestGatewayException"/>.
/// No personal data or secret is ever logged.
/// </summary>
public sealed class UpvestInvestmentGateway : IUpvestInvestmentGateway
{
    private const string DefaultSandboxIban = "DE89370400440532013000";

    private readonly UpvestInvestmentApiClient _client;
    private readonly UpvestSettings _settings;
    private readonly ILogger<UpvestInvestmentGateway> _logger;

    public UpvestInvestmentGateway(
        UpvestInvestmentApiClient client,
        IOptions<UpvestSettings> settings,
        ILogger<UpvestInvestmentGateway> logger)
    {
        _client = client;
        _settings = settings.Value;
        _logger = logger;
    }

    // ---------------------------------------------------------------- Flow 1: onboarding

    public Task<Guid> CreateUserAsync(InvestorRegistration reg, Guid idempotencyKey, CancellationToken ct) =>
        WriteCall(nameof(CreateUserAsync), async () =>
        {
            var body = new UserTolCreateRequest
            {
                FirstName = reg.FirstName,
                LastName = reg.LastName,
                Email = reg.Email,
                BirthDate = reg.BirthDate,
                BirthCity = reg.BirthCity,
                BirthCountry = ResolveBirthCountry(reg.BirthCountry),
                Nationalities = new[] { ResolveNationality(reg.Nationality) },
                PhoneNumber = SanitizePhone(reg.PhoneNumber),
                Address = new Address2
                {
                    AddressLine1 = reg.Address.Line1,
                    Postcode = reg.Address.Postcode,
                    City = reg.Address.City,
                    Country = ResolveCountry(reg.Address.Country),
                },
                Fatca = new Fatca { Status = false, ConfirmedAt = DateTimeOffset.UtcNow },
            };

            var response = await _client.Users.CreateUser(
                new CreateUserRequest { IdempotencyKey = idempotencyKey, Body = body },
                cancellationToken: ct);

            var id = response.Match(
                onUserByol: u => u.Id,
                onUserTol: u => u.Id,
                otherwise: _ => throw new UpvestGatewayException(
                    "Upvest returned an unrecognised user type.", HttpStatusCode.BadGateway));
            _logger.LogInformation("Upvest user created: {UserId}", id);
            return id;
        });

    public Task<Guid> CreateKycCheckAsync(Guid userId, InvestorRegistration reg, CancellationToken ct) =>
        WriteCall(nameof(CreateKycCheckAsync), async () =>
        {
            var body = new UserCheckKnowYourCustomerCreateRequest
            {
                CheckConfirmedAt = DateTimeOffset.UtcNow,
                // The shop verified the shopper; this points Upvest at the evidence record the shop holds.
                DataDownloadLink = $"{_settings.BaseUrl.TrimEnd('/')}/kyc-evidence/{userId}",
                DocumentType = ResolveDocumentType(reg.IdentityDocument.Type),
                DocumentExpirationDate = reg.IdentityDocument.ExpiryDate,
                Nationality = reg.IdentityDocument.IssuingCountry,
                Provider = "eShopOnWeb",
                Method = Method.ElectronicId,
            };

            var response = await _client.UserChecks.CreateUserCheck(
                new CreateUserCheckRequest { UserId = userId, Body = body },
                cancellationToken: ct);
            _logger.LogInformation("Upvest KYC check created: {CheckId} for user {UserId}", response.Id, userId);
            return response.Id;
        });

    public Task SetTaxResidencyAsync(Guid userId, string taxId, string taxCountry, Guid idempotencyKey, CancellationToken ct) =>
        WriteCall(nameof(SetTaxResidencyAsync), async () =>
        {
            var body = new TaxResidenciesSetRequest
            {
                TaxResidencies = new[]
                {
                    (TaxResidencyForCreateRequest)new WithTaxIdentifierNumber
                    {
                        Country = ResolveCountry(taxCountry),
                        TaxIdentifierNumber = taxId,
                    },
                },
            };
            await _client.TaxResidencies.SetTaxResidencies(
                new SetTaxResidenciesRequest { UserId = userId, IdempotencyKey = idempotencyKey, Body = body },
                cancellationToken: ct);
            _logger.LogInformation("Upvest tax residency set for user {UserId}", userId);
            return true;
        });

    public Task<Guid> CreateAccountGroupAsync(Guid userId, Guid idempotencyKey, CancellationToken ct) =>
        WriteCall(nameof(CreateAccountGroupAsync), async () =>
        {
            var body = new AccountGroupCreateUserRequest { UserId = userId, Type = Type13.Personal };
            var response = await _client.AccountGroups.CreateAccountGroup(
                new CreateAccountGroupRequest { IdempotencyKey = idempotencyKey, Body = body },
                cancellationToken: ct);

            var id = response.Match(
                onAccountGroup: g => g.Id,
                onBusinessAccountGroup: g => g.Id,
                otherwise: _ => throw new UpvestGatewayException(
                    "Upvest returned an unrecognised account group type.", HttpStatusCode.BadGateway));
            _logger.LogInformation("Upvest account group created: {AccountGroupId} for user {UserId}", id, userId);
            return id;
        });

    public Task<Guid> CreateAccountAsync(Guid userId, Guid accountGroupId, Guid idempotencyKey, CancellationToken ct) =>
        WriteCall(nameof(CreateAccountAsync), async () =>
        {
            var body = new AccountCreateUserRequest
            {
                UserId = userId,
                AccountGroupId = accountGroupId,
                Type = Type16.Portfolio, // the account able to hold a managed portfolio
            };
            var response = await _client.AccountsApi.CreateAccount(
                new CreateAccountRequest { IdempotencyKey = idempotencyKey, Body = body },
                cancellationToken: ct);

            var id = response.Match(
                onAccount: a => a.Id,
                onBusinessAccount: a => a.Id,
                otherwise: _ => throw new UpvestGatewayException(
                    "Upvest returned an unrecognised account type.", HttpStatusCode.BadGateway));
            _logger.LogInformation("Upvest portfolio account created: {AccountId} for user {UserId}", id, userId);
            return id;
        });

    public Task<InvestorState> GetInvestorStateAsync(Guid userId, Guid? kycCheckId, Guid? accountGroupId, CancellationToken ct) =>
        ReadCall(nameof(GetInvestorStateAsync), async () =>
        {
            Status6? kyc = null;
            if (kycCheckId.HasValue)
            {
                var check = await _client.UserChecks.RetrieveUserCheck(
                    new RetrieveUserCheckRequest { UserId = userId, CheckId = kycCheckId.Value },
                    cancellationToken: ct);
                if (check.TryGetUserCheckKnowYourCustomer(out var kycCheck))
                {
                    kyc = kycCheck.Status;
                }
            }

            Status18? group = null;
            if (accountGroupId.HasValue)
            {
                var grp = await _client.AccountGroups.RetrieveAccountGroup(
                    new RetrieveAccountGroupRequest { AccountGroupId = accountGroupId.Value },
                    cancellationToken: ct);
                if (grp.TryGetAccountGroup(out var ag))
                {
                    group = ag.Status;
                }
            }

            if (kyc == Status6.Failed || group == Status18.Closed || group == Status18.Locked)
                return InvestorState.Rejected;
            if (kyc == Status6.Passed && group == Status18.Active)
                return InvestorState.Active;
            return InvestorState.Pending;
        });

    // ---------------------------------------------------------------- Flow 2: models

    public Task<Guid> CreateAllocationAsync(string name, IReadOnlyList<UpvestFund> funds, Guid idempotencyKey, CancellationToken ct) =>
        WriteCall(nameof(CreateAllocationAsync), async () =>
        {
            var body = new PortfoliosAllocationCreateRequest { Name = name, Allocation = ToAllocation(funds) };
            var response = await _client.Portfolios.CreatePortfoliosAllocation(
                new CreatePortfoliosAllocationRequest { IdempotencyKey = idempotencyKey, Body = body },
                cancellationToken: ct);
            _logger.LogInformation("Upvest allocation created: {AllocationId}", response.Id);
            return response.Id;
        });

    public Task UpdateAllocationAsync(Guid allocationId, string name, IReadOnlyList<UpvestFund> funds, CancellationToken ct) =>
        WriteCall(nameof(UpdateAllocationAsync), async () =>
        {
            var body = new PortfoliosAllocationUpdateRequest { Name = name, Allocation = ToAllocation(funds) };
            await _client.Portfolios.UpdatePortfoliosAllocation(
                new UpdatePortfoliosAllocationRequest { AllocationId = allocationId, Body = body },
                cancellationToken: ct);
            _logger.LogInformation("Upvest allocation updated: {AllocationId}", allocationId);
            return true;
        });

    public Task<IReadOnlyList<UpvestAllocation>> ListAllocationsAsync(CancellationToken ct) =>
        ReadCall(nameof(ListAllocationsAsync), async () =>
        {
            var results = new List<UpvestAllocation>();
            int offset = 0;
            const int limit = 100;
            const int maxPages = 100;
            for (int page = 0; page < maxPages; page++)
            {
                var response = await _client.Portfolios.ListPortfoliosAllocations(
                    new ListPortfoliosAllocationsRequest { Offset = offset, Limit = limit },
                    cancellationToken: ct);

                foreach (var a in response.Data)
                {
                    var funds = new List<UpvestFund>();
                    foreach (var alloc in a.Allocation)
                    {
                        funds.Add(new UpvestFund(InstrumentIdToString(alloc.InstrumentId), ParseDecimal(alloc.Weight)));
                    }
                    results.Add(new UpvestAllocation(a.Id, a.Name, funds));
                }

                offset = response.Meta.Offset + response.Meta.Count;
                if (response.Meta.Count == 0 || offset >= response.Meta.TotalCount)
                    break;
            }
            return (IReadOnlyList<UpvestAllocation>)results;
        });

    public Task SetConfigurationAsync(Guid accountId, Guid allocationId, bool isSwitch, Guid idempotencyKey, CancellationToken ct) =>
        WriteCall(nameof(SetConfigurationAsync), async () =>
        {
            if (isSwitch)
            {
                var body = new PortfoliosConfigurationUpdateRequest { AllocationId = allocationId };
                await _client.Portfolios.UpdatePortfoliosConfiguration(
                    new UpdatePortfoliosConfigurationRequest { AccountId = accountId, IdempotencyKey = idempotencyKey, Body = body },
                    cancellationToken: ct);
            }
            else
            {
                var body = new PortfoliosConfigurationCreateRequest { AccountId = accountId, AllocationId = allocationId };
                await _client.Portfolios.CreatePortfoliosConfiguration(
                    new CreatePortfoliosConfigurationRequest { IdempotencyKey = idempotencyKey, Body = body },
                    cancellationToken: ct);
            }
            _logger.LogInformation("Upvest portfolio configuration set for account {AccountId}", accountId);
            return true;
        });

    // ---------------------------------------------------------------- Flow 3: pay in

    public Task<TopupResult> CreateTopupAsync(Guid accountGroupId, decimal amount, Guid idempotencyKey, CancellationToken ct) =>
        WriteCall(nameof(CreateTopupAsync), async () =>
        {
            var body = new PaymentsTopUpCreateRequest
            {
                AccountGroupId = accountGroupId,
                CashAmount = FormatAmount(amount),
                Currency = Currency.Eur,
            };
            var response = await _client.TopUps.CreateTopup(
                new CreateTopupRequest { IdempotencyKey = idempotencyKey, Body = body },
                cancellationToken: ct);
            _logger.LogInformation("Upvest top-up created: {TopupId}", response.Id);
            return new TopupResult(response.Id, response.SettlementReference);
        });

    public Task SimulateInboundTransferAsync(decimal amount, string clientReference, string? settlementReference, Guid idempotencyKey, CancellationToken ct) =>
        WriteCall(nameof(SimulateInboundTransferAsync), async () =>
        {
            var iban = string.IsNullOrWhiteSpace(_settings.SandboxCollectionIban)
                ? DefaultSandboxIban
                : _settings.SandboxCollectionIban!;
            var body = new PaymentsBankTransactionCreateRequest
            {
                Account = new Account29 { Iban = iban },
                Amount = FormatAmount(amount),
                Currency = BankTransactionCurrency.Eur,
                ClientReference = clientReference,
                CounterpartyName = "eShopOnWeb Investor",
                CounterpartyAccount = new CounterpartyAccount { Iban = DefaultSandboxIban },
                RemittanceInformation = settlementReference,
            };
            await _client.Tests.CreateBankTransaction(
                new CreateBankTransactionRequest { IdempotencyKey = idempotencyKey, Body = body },
                cancellationToken: ct);
            _logger.LogInformation("Upvest sandbox inbound transfer simulated for reference {Reference}", clientReference);
            return true;
        });

    public Task<Guid> PlaceBuyOrderAsync(Guid userId, Guid accountId, decimal amount, Guid idempotencyKey, CancellationToken ct) =>
        WriteCall(nameof(PlaceBuyOrderAsync), async () =>
        {
            var body = new PortfoliosOrderPlaceRequest
            {
                UserId = userId,
                AccountId = accountId,
                CashAmount = FormatAmount(amount),
                Currency = Currency.Eur,
                Side = Side15.Buy,
            };
            var response = await _client.Portfolios.CreatePortfoliosOrder(
                new CreatePortfoliosOrderRequest { IdempotencyKey = idempotencyKey, Body = body },
                cancellationToken: ct);
            _logger.LogInformation("Upvest portfolio order placed: {OrderId}", response.Id);
            return response.Id;
        });

    public Task<PaymentState> GetPaymentStateAsync(Guid? topupId, Guid? orderId, CancellationToken ct) =>
        ReadCall(nameof(GetPaymentStateAsync), async () =>
        {
            if (orderId.HasValue)
            {
                var order = await _client.Portfolios.RetrievePortfoliosOrder(
                    new RetrievePortfoliosOrderRequest { PortfolioOrderId = orderId.Value },
                    cancellationToken: ct);
                if (order.Status == Status65.Filled || order.Status == Status65.Settled)
                    return PaymentState.Invested;
                if (order.Status == Status65.Cancelled)
                    return PaymentState.Failed;
                return PaymentState.Pending;
            }

            if (topupId.HasValue)
            {
                var topup = await _client.TopUps.RetrieveTopup(
                    new RetrieveTopupRequest { TopupId = topupId.Value },
                    cancellationToken: ct);
                if (topup.Status == Status29.Cancelled)
                    return PaymentState.Failed;
            }
            return PaymentState.Pending;
        });

    // ---------------------------------------------------------------- Flow 4: realignment

    public Task<Guid> TriggerRealignmentAsync(Guid allocationId, Guid idempotencyKey, CancellationToken ct) =>
        WriteCall(nameof(TriggerRealignmentAsync), async () =>
        {
            var body = (TriggerPortfolioRebalancingRequest)new Allocations { AllocationsValue = new[] { allocationId } };
            var response = await _client.PortfoliosRebalancing.TriggerPortfolioRebalancing(
                new TriggerPortfolioRebalancingOperationRequest { IdempotencyKey = idempotencyKey, Body = body },
                cancellationToken: ct);
            _logger.LogInformation("Upvest realignment triggered: {ExecutionId} for allocation {AllocationId}", response.Id, allocationId);
            return response.Id;
        });

    public Task<RealignmentState> GetRealignmentStateAsync(Guid executionId, CancellationToken ct) =>
        ReadCall(nameof(GetRealignmentStateAsync), async () =>
        {
            int offset = 0;
            const int limit = 100;
            const int maxPages = 100;
            bool any = false;
            bool allFilled = true;
            bool anyCancelled = false;

            for (int page = 0; page < maxPages; page++)
            {
                var response = await _client.PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders(
                    new ListPortfolioRebalancingExecutionOrdersRequest { ExecutionId = executionId, Offset = offset, Limit = limit },
                    cancellationToken: ct);

                foreach (var order in response.Data)
                {
                    any = true;
                    if (order.Status == Status71.Cancelled) anyCancelled = true;
                    if (order.Status != Status71.Filled) allFilled = false;
                }

                offset = response.Meta.Offset + response.Meta.Count;
                if (response.Meta.Count == 0 || offset >= response.Meta.TotalCount)
                    break;
            }

            if (anyCancelled) return RealignmentState.Failed;
            if (any && allFilled) return RealignmentState.Completed;
            return RealignmentState.Pending;
        });

    // ---------------------------------------------------------------- Flow 5: portfolio

    public Task<UpvestPortfolio> GetPortfolioAsync(Guid accountId, CancellationToken ct) =>
        ReadCall(nameof(GetPortfolioAsync), async () =>
        {
            var valuation = await _client.Valuations.GetAccountValuation(
                new GetAccountValuationRequest { AccountId = accountId, PriceQuality = PriceQuality3.HighestAvailable },
                cancellationToken: ct);

            var holdings = new List<UpvestHolding>();
            foreach (var position in valuation.SecurityPositions)
            {
                var instrumentId = position.Instrument.Isin ?? position.Instrument.Uuid.ToString();
                var quantity = ParseDecimal(position.Quantity);
                var value = position.Value is null ? 0m : ParseDecimal(position.Value.Amount);
                holdings.Add(new UpvestHolding(instrumentId, quantity, value));
            }
            var total = ParseDecimal(valuation.TotalSecurityValue.Amount);
            return new UpvestPortfolio(holdings, total);
        });

    // ---------------------------------------------------------------- helpers

    private static IReadOnlyList<Allocation> ToAllocation(IReadOnlyList<UpvestFund> funds)
    {
        var list = new List<Allocation>(funds.Count);
        foreach (var f in funds)
        {
            list.Add(new Allocation
            {
                InstrumentId = f.InstrumentId, // ISIN string → InstrumentId union via implicit operator
                InstrumentIdType = InstrumentIdType4.Isin,
                Weight = f.Weight.ToString(CultureInfo.InvariantCulture),
            });
        }
        return list;
    }

    private static string InstrumentIdToString(InstrumentId id) =>
        id.TryGetString(out var s) ? s! : (id.TryGetGuid(out var g) ? g.ToString() : string.Empty);

    private static string FormatAmount(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Upvest requires a phone number of 8–15 digits with no separators or country prefix
    /// symbol. Strip everything but digits; omit it if it does not fit the pattern.</summary>
    private static string? SanitizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(Array.FindAll(phone.ToCharArray(), char.IsDigit));
        return digits.Length is >= 8 and <= 15 ? digits : null;
    }

    private static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;

    private static Nationality ResolveNationality(string code)
    {
        if (Nationality.TryGetKnownValue(code?.ToUpperInvariant() ?? string.Empty, out var v))
            return v;
        throw new UpvestGatewayException($"Unsupported nationality '{code}'.", HttpStatusCode.BadRequest);
    }

    private static Country ResolveCountry(string code)
    {
        if (Country.TryGetKnownValue(code?.ToUpperInvariant() ?? string.Empty, out var v))
            return v;
        throw new UpvestGatewayException($"Unsupported country '{code}'.", HttpStatusCode.BadRequest);
    }

    private static BirthCountry? ResolveBirthCountry(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;
        if (BirthCountry.TryGetKnownValue(code.ToUpperInvariant(), out var v))
            return v;
        throw new UpvestGatewayException($"Unsupported birth country '{code}'.", HttpStatusCode.BadRequest);
    }

    private static DocumentType3 ResolveDocumentType(string type)
    {
        var normalized = (type ?? string.Empty).Trim().ToUpperInvariant().Replace(' ', '_').Replace('-', '_');
        if (DocumentType3.TryGetKnownValue(normalized, out var v))
            return v;
        throw new UpvestGatewayException($"Unsupported identity document type '{type}'.", HttpStatusCode.BadRequest);
    }

    private async Task<T> ReadCall<T>(string op, Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (Exception ex)
        {
            throw Translate(op, ex, isWrite: false);
        }
    }

    private async Task<T> WriteCall<T>(string op, Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (Exception ex)
        {
            throw Translate(op, ex, isWrite: true);
        }
    }

    /// <summary>
    /// Translates every SDK failure into one <see cref="UpvestGatewayException"/>. A validation
    /// error we raised ourselves passes through unchanged. Transport failures on a write are marked
    /// as an unknown outcome so the caller keeps the record pending and reconciles on the next read.
    /// </summary>
    private UpvestGatewayException Translate(string op, Exception ex, bool isWrite)
    {
        switch (ex)
        {
            case UpvestGatewayException gw:
                return gw; // a validation error we raised; keep its status

            case ResponseDeserializationException de:
                // A 2xx we could not read is a genuine unknown; a non-2xx keeps its status.
                var unknown2xx = (int)de.StatusCode is >= 200 and < 300;
                _logger.LogWarning("Upvest {Op} returned an unreadable response ({Status}).", op, de.StatusCode);
                return new UpvestGatewayException(
                    "The investment provider returned a response that could not be processed.",
                    de.StatusCode, de, outcomeUnknown: isWrite && unknown2xx);

            case ApiException api:
                // The provider answered with an error status. The SDK message strips the query and
                // carries no body, so it is safe to log; we never log the request body.
                _logger.LogWarning("Upvest {Op} failed with status {Status}.", op, api.StatusCode);
                return new UpvestGatewayException(
                    $"The investment provider rejected the request ({(int)api.StatusCode}).",
                    api.StatusCode, api);

            case AuthSchemeException ae:
                _logger.LogError(ae, "Upvest {Op} failed: credentials missing or rejected.", op);
                return new UpvestGatewayException(
                    "The investment provider credentials are missing or were rejected.",
                    HttpStatusCode.BadGateway, ae);

            case SdkTimeoutException te:
                _logger.LogWarning("Upvest {Op} timed out after {Timeout}.", op, te.Timeout);
                return new UpvestGatewayException(
                    "The investment provider did not respond in time.",
                    HttpStatusCode.GatewayTimeout, te, outcomeUnknown: isWrite);

            case SdkConnectionException ce:
                _logger.LogWarning(ce, "Upvest {Op} could not reach the provider.", op);
                return new UpvestGatewayException(
                    "The investment provider could not be reached.",
                    HttpStatusCode.BadGateway, ce, outcomeUnknown: isWrite);

            case SdkException se:
                _logger.LogError(se, "Upvest {Op} failed.", op);
                return new UpvestGatewayException(
                    "The investment provider request failed.",
                    HttpStatusCode.BadGateway, se, outcomeUnknown: isWrite);

            default:
                _logger.LogError(ex, "Unexpected failure calling Upvest {Op}.", op);
                return new UpvestGatewayException(
                    "An unexpected error occurred calling the investment provider.",
                    HttpStatusCode.BadGateway, ex, outcomeUnknown: isWrite);
        }
    }
}
