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
using Microsoft.Extensions.Logging;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.ErrorResponse;
using UpvestInvestmentApi.Core.Exceptions;
using UpvestInvestmentApi.Errors;
using UpvestInvestmentApi.Models;
using UpvestInvestmentApi.Models.AnyOf;
using UpvestInvestmentApi.Models.Enums;
using UpvestInvestmentApi.Requests.AccountGroups;
using UpvestInvestmentApi.Requests.AccountsApi;
using UpvestInvestmentApi.Requests.Portfolios;
using UpvestInvestmentApi.Requests.PortfoliosRebalancing;
using UpvestInvestmentApi.Requests.Tests;
using UpvestInvestmentApi.Requests.Users;
using UpvestInvestmentApi.Requests.Valuations;
using UpvestInvestmentApi.Requests.VirtualBankAccounts;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The Upvest SDK implementation of <see cref="IUpvestPortfolioGateway"/>. Every call is wrapped in a uniform
/// boundary that translates SDK failures into <see cref="UpvestIntegrationException"/> (carrying the provider
/// status), replays a write once on a connection failure using the same idempotency key, and never writes
/// personal data to logs.
/// </summary>
public class UpvestPortfolioGateway : IUpvestPortfolioGateway
{
    // A valid-format placeholder funding counterparty for the sandbox "money in" test credit.
    private const string FundingCounterpartyName = "eShopOnWeb Funding";
    private const string FundingCounterpartyIban = "DE89370400440532013000";

    private readonly UpvestInvestmentApiClient _client;
    private readonly ILogger<UpvestPortfolioGateway> _logger;

    public UpvestPortfolioGateway(UpvestInvestmentApiClient client, ILogger<UpvestPortfolioGateway> logger)
    {
        _client = client;
        _logger = logger;
    }

    // --- Flow 1 -------------------------------------------------------------------------------------------

    public async Task<Guid> CreateInvestorUserAsync(InvestorRegistration registration, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var body = BuildUserBody(registration);
        var response = await ExecuteAsync<UserCreateRequest1, CreateUserError>(
            ct => _client.Users.CreateUser(new CreateUserRequest { IdempotencyKey = idempotencyKey, Body = body }, cancellationToken: ct),
            "create user", isWrite: true, cancellationToken);

        return response.Match(
            onUserByol: u => u.Id,
            onUserTol: u => u.Id,
            otherwise: _ => throw new UpvestIntegrationException("Upvest returned an unrecognised user shape.", HttpStatusCode.BadGateway));
    }

    public async Task<Guid> CreateAccountGroupAsync(Guid userId, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var body = AccountGroupCreateRequest.AccountGroupCreateUserRequest(
            new AccountGroupCreateUserRequest { UserId = userId, Type = Type13.Personal });

        var response = await ExecuteAsync<AccountGroupCreateResponse, CreateAccountGroupError>(
            ct => _client.AccountGroups.CreateAccountGroup(new CreateAccountGroupRequest { IdempotencyKey = idempotencyKey, Body = body }, cancellationToken: ct),
            "create account group", isWrite: true, cancellationToken);

        return response.Match(
            onAccountGroup: g => g.Id,
            onBusinessAccountGroup: _ => throw new UpvestIntegrationException("Upvest returned a business account group where a personal one was expected.", HttpStatusCode.BadGateway),
            otherwise: _ => throw new UpvestIntegrationException("Upvest returned an unrecognised account-group shape.", HttpStatusCode.BadGateway));
    }

    public async Task<Guid> CreatePortfolioAccountAsync(Guid userId, Guid accountGroupId, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var body = AccountCreateRequest.AccountCreateUserRequest(
            new AccountCreateUserRequest { UserId = userId, AccountGroupId = accountGroupId, Type = Type16.Portfolio });

        var response = await ExecuteAsync<AccountCreateResponse, CreateAccountError>(
            ct => _client.AccountsApi.CreateAccount(new CreateAccountRequest { IdempotencyKey = idempotencyKey, Body = body }, cancellationToken: ct),
            "create account", isWrite: true, cancellationToken);

        return response.Match(
            onAccount: a => a.Id,
            onBusinessAccount: _ => throw new UpvestIntegrationException("Upvest returned a business account where a personal one was expected.", HttpStatusCode.BadGateway),
            otherwise: _ => throw new UpvestIntegrationException("Upvest returned an unrecognised account shape.", HttpStatusCode.BadGateway));
    }

    public async Task<InvestorStatus> GetInvestorStatusAsync(Guid userId, Guid? accountGroupId, Guid? accountId, CancellationToken cancellationToken)
    {
        var userResponse = await ExecuteAsync<UserGetResponse, RetrieveUserError>(
            ct => _client.Users.RetrieveUser(new UpvestInvestmentApi.Requests.Users.RetrieveUserRequest { UserId = userId }, cancellationToken: ct),
            "retrieve user", isWrite: false, cancellationToken);

        var userStatus = userResponse.Match(
            onUserByol: u => u.Status.Value,
            onUserTol: u => u.Status.Value,
            otherwise: _ => "UNKNOWN");

        if (userStatus is "OFFBOARDED" or "OFFBOARDING")
        {
            return InvestorStatus.Rejected;
        }

        // Readiness needs the account group and account to be ACTIVE. Only read what we have ids for.
        var groupReady = false;
        if (accountGroupId.HasValue)
        {
            var groupResponse = await ExecuteAsync<AccountGroupRetrieveResponse, RetrieveAccountGroupError>(
                ct => _client.AccountGroups.RetrieveAccountGroup(new UpvestInvestmentApi.Requests.AccountGroups.RetrieveAccountGroupRequest { AccountGroupId = accountGroupId.Value }, cancellationToken: ct),
                "retrieve account group", isWrite: false, cancellationToken);

            var groupStatus = groupResponse.Match(
                onAccountGroup: g => g.Status.Value,
                onBusinessAccountGroup: _ => "UNKNOWN",
                otherwise: _ => "UNKNOWN");

            if (groupStatus is "CLOSED" or "LOCKED")
            {
                return InvestorStatus.Rejected;
            }

            groupReady = groupStatus == "ACTIVE";
        }

        var accountReady = false;
        if (accountId.HasValue)
        {
            var accountResponse = await ExecuteAsync<AccountRetrieveResponse, RetrieveAccountError>(
                ct => _client.AccountsApi.RetrieveAccount(new UpvestInvestmentApi.Requests.AccountsApi.RetrieveAccountRequest { AccountId = accountId.Value }, cancellationToken: ct),
                "retrieve account", isWrite: false, cancellationToken);

            var accountStatus = accountResponse.Match(
                onAccount: a => a.Status.Value,
                onBusinessAccount: _ => "UNKNOWN",
                otherwise: _ => "UNKNOWN");

            if (accountStatus is "CLOSED" or "LOCKED")
            {
                return InvestorStatus.Rejected;
            }

            accountReady = accountStatus == "ACTIVE";
        }

        return groupReady && accountReady ? InvestorStatus.Active : InvestorStatus.Pending;
    }

    // --- Flow 2 -------------------------------------------------------------------------------------------

    public async Task<Guid> CreateModelAllocationAsync(string name, IReadOnlyList<FundWeight> funds, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var body = new PortfoliosAllocationCreateRequest { Name = name, Allocation = BuildAllocation(funds) };

        var allocation = await ExecuteAsync<PortfoliosAllocation, CreatePortfoliosAllocationError>(
            ct => _client.Portfolios.CreatePortfoliosAllocation(new CreatePortfoliosAllocationRequest { IdempotencyKey = idempotencyKey, Body = body }, cancellationToken: ct),
            "create allocation", isWrite: true, cancellationToken);

        return allocation.Id;
    }

    public async Task UpdateModelAllocationAsync(Guid allocationId, string name, IReadOnlyList<FundWeight> funds, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var body = new PortfoliosAllocationUpdateRequest { Name = name, Allocation = BuildAllocation(funds) };

        await ExecuteAsync<PortfoliosAllocation, UpdatePortfoliosAllocationError>(
            ct => _client.Portfolios.UpdatePortfoliosAllocation(new UpdatePortfoliosAllocationRequest { AllocationId = allocationId, Body = body }, cancellationToken: ct),
            "update allocation", isWrite: true, cancellationToken);
    }

    public async Task<IReadOnlyList<UpvestAllocation>> ListModelAllocationsAsync(CancellationToken cancellationToken)
    {
        var results = new List<UpvestAllocation>();
        var offset = 0;
        const int limit = 100;

        while (true)
        {
            var page = await ExecuteAsync<PortfoliosAllocationsListResponse, ListPortfoliosAllocationsError>(
                ct => _client.Portfolios.ListPortfoliosAllocations(new ListPortfoliosAllocationsRequest { Offset = offset, Limit = limit }, cancellationToken: ct),
                "list allocations", isWrite: false, cancellationToken);

            results.AddRange(page.Data.Select(a => new UpvestAllocation(
                a.Id, a.Name,
                a.Allocation.Select(x => new FundWeight(ReadInstrumentId(x.InstrumentId), ParseDecimal(x.Weight))).ToList())));

            if (page.Meta.Offset + page.Meta.Count >= page.Meta.TotalCount || page.Meta.Count == 0)
            {
                break;
            }

            offset += page.Meta.Count;
        }

        return results;
    }

    public async Task SetFollowedModelAsync(Guid accountId, Guid allocationId, bool alreadyFollowingAModel, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        if (alreadyFollowingAModel)
        {
            var body = new PortfoliosConfigurationUpdateRequest { AllocationId = allocationId };
            await ExecuteAsync<PortfoliosConfiguration, UpdatePortfoliosConfigurationError>(
                ct => _client.Portfolios.UpdatePortfoliosConfiguration(new UpdatePortfoliosConfigurationRequest { AccountId = accountId, IdempotencyKey = idempotencyKey, Body = body }, cancellationToken: ct),
                "update portfolio configuration", isWrite: true, cancellationToken);
        }
        else
        {
            var body = new PortfoliosConfigurationCreateRequest { AccountId = accountId, AllocationId = allocationId };
            await ExecuteAsync<PortfoliosConfiguration, CreatePortfoliosConfigurationError>(
                ct => _client.Portfolios.CreatePortfoliosConfiguration(new CreatePortfoliosConfigurationRequest { IdempotencyKey = idempotencyKey, Body = body }, cancellationToken: ct),
                "create portfolio configuration", isWrite: true, cancellationToken);
        }
    }

    public async Task<Guid?> GetFollowedAllocationIdAsync(Guid accountId, CancellationToken cancellationToken)
    {
        try
        {
            var config = await ExecuteAsync<PortfoliosConfiguration, RetrievePortfoliosConfigurationError>(
                ct => _client.Portfolios.RetrievePortfoliosConfiguration(new RetrievePortfoliosConfigurationRequest { AccountId = accountId }, cancellationToken: ct),
                "retrieve portfolio configuration", isWrite: false, cancellationToken);
            return config.AllocationId;
        }
        catch (UpvestIntegrationException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    // --- Flow 3 -------------------------------------------------------------------------------------------

    public async Task<string> EnsureVirtualBankAccountIbanAsync(Guid accountGroupId, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var offset = 0;
        const int limit = 100;
        while (true)
        {
            var page = await ExecuteAsync<PaymentsVirtualBankAccountsListResponse, ListVirtualBankAccountsError>(
                ct => _client.VirtualBankAccounts.ListVirtualBankAccounts(new ListVirtualBankAccountsRequest { AccountGroupId = accountGroupId, Offset = offset, Limit = limit }, cancellationToken: ct),
                "list virtual bank accounts", isWrite: false, cancellationToken);

            var iban = page.Data
                .Select(d => d.Identification.Swift?.Iban)
                .FirstOrDefault(i => !string.IsNullOrEmpty(i));
            if (!string.IsNullOrEmpty(iban))
            {
                return iban!;
            }

            if (page.Meta.Offset + page.Meta.Count >= page.Meta.TotalCount || page.Meta.Count == 0)
            {
                break;
            }

            offset += page.Meta.Count;
        }

        var created = await ExecuteAsync<PaymentsVirtualBankAccountsResponse, CreateVirtualBankAccountError>(
            ct => _client.VirtualBankAccounts.CreateVirtualBankAccount(
                new CreateVirtualBankAccountRequest
                {
                    IdempotencyKey = idempotencyKey,
                    Body = new PaymentsVirtualBankAccountCreateRequest { AccountGroupId = accountGroupId, Name = "eShopOnWeb portfolio funding" }
                }, cancellationToken: ct),
            "create virtual bank account", isWrite: true, cancellationToken);

        var createdIban = created.Identification.Swift?.Iban;
        if (string.IsNullOrEmpty(createdIban))
        {
            throw new UpvestIntegrationException("Upvest created a virtual bank account without an IBAN.", HttpStatusCode.BadGateway);
        }

        return createdIban!;
    }

    public async Task CreditCashAsync(string iban, decimal amount, string clientReference, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var body = new PaymentsBankTransactionCreateRequest
        {
            Account = new Account29 { Iban = iban },
            Amount = FormatAmount(amount),
            Currency = BankTransactionCurrency.Eur,
            ClientReference = ToClientReference(clientReference),
            CounterpartyName = FundingCounterpartyName,
            CounterpartyAccount = new CounterpartyAccount { Iban = FundingCounterpartyIban }
        };

        await ExecuteAsync<PaymentsBankTransactionCreateResponse, CreateBankTransactionError>(
            ct => _client.Tests.CreateBankTransaction(new CreateBankTransactionRequest { IdempotencyKey = idempotencyKey, Body = body }, cancellationToken: ct),
            "credit cash", isWrite: true, cancellationToken);
    }

    public async Task<UpvestOrderResult> PlaceBuyOrderAsync(Guid userId, Guid accountId, decimal amount, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var body = new PortfoliosOrderPlaceRequest
        {
            UserId = userId,
            AccountId = accountId,
            CashAmount = FormatAmount(amount),
            Currency = Currency.Eur,
            Side = Side15.Buy
        };

        var order = await ExecuteAsync<PortfoliosOrder, CreatePortfoliosOrderError>(
            ct => _client.Portfolios.CreatePortfoliosOrder(new CreatePortfoliosOrderRequest { IdempotencyKey = idempotencyKey, Body = body }, cancellationToken: ct),
            "place portfolio order", isWrite: true, cancellationToken);

        return new UpvestOrderResult(order.Id, MapOrderStatus(order.Status));
    }

    public async Task<PaymentStatus> GetOrderStatusAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await ExecuteAsync<PortfoliosOrder, RetrievePortfoliosOrderError>(
            ct => _client.Portfolios.RetrievePortfoliosOrder(new RetrievePortfoliosOrderRequest { PortfolioOrderId = orderId }, cancellationToken: ct),
            "retrieve portfolio order", isWrite: false, cancellationToken);

        return MapOrderStatus(order.Status);
    }

    // --- Flow 4 -------------------------------------------------------------------------------------------

    public async Task<Guid> TriggerRealignmentAsync(Guid allocationId, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var body = TriggerPortfolioRebalancingRequest.Allocations(new Allocations { AllocationsValue = new[] { allocationId } });

        var response = await ExecuteAsync<TriggerPortfolioRebalancingResponse, TriggerPortfolioRebalancingError>(
            ct => _client.PortfoliosRebalancing.TriggerPortfolioRebalancing(new TriggerPortfolioRebalancingOperationRequest { IdempotencyKey = idempotencyKey, Body = body }, cancellationToken: ct),
            "trigger realignment", isWrite: true, cancellationToken);

        return response.Id;
    }

    public async Task<RealignmentStatus> GetRealignmentStatusAsync(Guid realignmentId, CancellationToken cancellationToken)
    {
        // Confirms the execution exists; its status is derived from its execution orders below.
        await ExecuteAsync<PortfoliosRebalancingExecution, RetrievePortfoliosRebalancingExecutionError>(
            ct => _client.PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution(new RetrievePortfoliosRebalancingExecutionRequest { ExecutionId = realignmentId }, cancellationToken: ct),
            "retrieve realignment execution", isWrite: false, cancellationToken);

        var orders = new List<PortfoliosRebalancingExecutionOrder>();
        var offset = 0;
        const int limit = 100;
        while (true)
        {
            var page = await ExecuteAsync<PortfoliosRebalancingExecutionOrderListResponse, ListPortfolioRebalancingExecutionOrdersError>(
                ct => _client.PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders(new ListPortfolioRebalancingExecutionOrdersRequest { ExecutionId = realignmentId, Offset = offset, Limit = limit }, cancellationToken: ct),
                "list realignment orders", isWrite: false, cancellationToken);

            orders.AddRange(page.Data);

            if (page.Meta.Offset + page.Meta.Count >= page.Meta.TotalCount || page.Meta.Count == 0)
            {
                break;
            }

            offset += page.Meta.Count;
        }

        return AggregateRealignmentStatus(orders);
    }

    // --- Flow 5 -------------------------------------------------------------------------------------------

    public async Task<PortfolioHoldings> GetPortfolioHoldingsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var valuation = await ExecuteAsync<AccountValuation, GetAccountValuationError>(
            ct => _client.Valuations.GetAccountValuation(new GetAccountValuationRequest { AccountId = accountId, PriceQuality = PriceQuality3.HighestAvailable }, cancellationToken: ct),
            "get account valuation", isWrite: false, cancellationToken);

        var holdings = valuation.SecurityPositions
            .Select(p => new Holding(
                p.Instrument.Isin ?? p.Instrument.Uuid.ToString(),
                ParseDecimal(p.Quantity),
                p.Value is null ? 0m : ParseDecimal(p.Value.Amount)))
            .ToList();

        var total = ParseDecimal(valuation.TotalSecurityValue.Amount);
        var currency = valuation.TotalSecurityValue.Currency.Value;

        return new PortfolioHoldings(holdings, total, currency);
    }

    // --- Mapping helpers ----------------------------------------------------------------------------------

    private static UserCreateRequest BuildUserBody(InvestorRegistration registration)
    {
        if (!Country.TryGetKnownValue(registration.Address.Country, out var country))
        {
            throw new PortfolioRequestException($"Address country '{registration.Address.Country}' is not a supported country code.", 400);
        }

        if (!Nationality.TryGetKnownValue(registration.Nationality, out var nationality))
        {
            throw new PortfolioRequestException($"Nationality '{registration.Nationality}' is not a supported country code.", 400);
        }

        var tol = new UserTolCreateRequest
        {
            FirstName = registration.FirstName,
            LastName = registration.LastName,
            Email = registration.Email,
            BirthDate = registration.BirthDate,
            BirthCity = string.IsNullOrWhiteSpace(registration.BirthCity) ? null : registration.BirthCity,
            Nationalities = new[] { nationality },
            PhoneNumber = string.IsNullOrWhiteSpace(registration.PhoneNumber) ? null : registration.PhoneNumber,
            Address = new Address2
            {
                AddressLine1 = registration.Address.Line1,
                Postcode = registration.Address.Postcode,
                City = registration.Address.City,
                Country = country
            },
            // The shop collects taxCountry; a US tax country maps to FATCA status true.
            Fatca = new Fatca
            {
                Status = string.Equals(registration.TaxCountry, "US", StringComparison.OrdinalIgnoreCase),
                ConfirmedAt = DateTimeOffset.UtcNow
            }
        };

        if (!string.IsNullOrWhiteSpace(registration.BirthCountry)
            && BirthCountry.TryGetKnownValue(registration.BirthCountry, out var birthCountry))
        {
            tol = tol with { BirthCountry = birthCountry };
        }

        return UserCreateRequest.UserTolCreateRequest(tol);
    }

    private static IReadOnlyList<Allocation> BuildAllocation(IReadOnlyList<FundWeight> funds) =>
        funds.Select(f => new Allocation
        {
            InstrumentId = InstrumentId.String(f.InstrumentId),
            InstrumentIdType = InstrumentIdType4.Isin,
            Weight = FormatWeight(f.Weight)
        }).ToList();

    private static string ReadInstrumentId(InstrumentId instrumentId) =>
        instrumentId.Match(onString: s => s, onGuid: g => g.ToString(), otherwise: _ => string.Empty);

    internal static PaymentStatus MapOrderStatus(Status65 status) => status.Value switch
    {
        "FILLED" or "SETTLED" => PaymentStatus.Invested,
        "CANCELLED" => PaymentStatus.Failed,
        _ => PaymentStatus.Pending
    };

    internal static RealignmentStatus AggregateRealignmentStatus(IReadOnlyList<PortfoliosRebalancingExecutionOrder> orders)
    {
        if (orders.Count == 0)
        {
            return RealignmentStatus.Pending;
        }

        if (orders.Any(o => o.Status.Value is "NEW" or "PROCESSING"))
        {
            return RealignmentStatus.Pending;
        }

        // All terminal. A CANCELLED order fails the realignment unless it was cancelled because the portfolio
        // was already balanced (a benign outcome).
        var hasRealFailure = orders.Any(o =>
            o.Status.Value == "CANCELLED" &&
            !(o.CancellationReason is { } reason && reason == "PORTFOLIO_IS_BALANCED"));

        return hasRealFailure ? RealignmentStatus.Failed : RealignmentStatus.Completed;
    }

    internal static string FormatAmount(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    internal static string FormatWeight(decimal weight) =>
        weight == 100m ? "100" : weight.ToString("0.#####", CultureInfo.InvariantCulture);

    private static decimal ParseDecimal(string value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;

    // A payment id as a <=35-char, hyphen-free client reference acceptable to the bank-transaction endpoint.
    internal static string ToClientReference(string reference) =>
        Guid.TryParse(reference, out var g) ? g.ToString("N") : reference.Replace("-", string.Empty);

    // --- Error boundary -----------------------------------------------------------------------------------

    /// <summary>
    /// Runs one SDK call, translating every SDK failure into <see cref="UpvestIntegrationException"/>. A write is
    /// replayed once on a connection failure with the same idempotency key (so a possible first effect is not
    /// duplicated); if it still cannot reach the provider, the outcome is reported as unknown.
    /// </summary>
    private async Task<T> ExecuteAsync<T, TError>(Func<CancellationToken, Task<T>> call, string operation, bool isWrite, CancellationToken cancellationToken)
        where TError : ApiError
    {
        var maxAttempts = isWrite ? 2 : 1;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await call(cancellationToken);
            }
            catch (ApiException<TError> ex)
            {
                throw TranslateApiException(ex, operation);
            }
            catch (ResponseDeserializationException ex)
            {
                _logger.LogWarning("Upvest {Operation} returned a body that could not be processed (HTTP {Status}).", operation, (int)ex.StatusCode);
                throw new UpvestIntegrationException($"The investment provider returned an unprocessable response for '{operation}'.", ex.StatusCode, outcomeUnknown: false, ex);
            }
            catch (AuthSchemeException ex)
            {
                // The token request for this operation's scope was refused. In this sandbox that is expected
                // for every scope beyond user creation; log concisely (no stack) and surface it as unavailable.
                _logger.LogWarning("Upvest {Operation}: not authenticated for this operation's scope ({Reason}).", operation, ex.Message);
                throw new UpvestIntegrationException("Investment-provider credentials are not authorised for this operation.", statusCode: null, outcomeUnknown: false, ex);
            }
            catch (SdkException ex) when (ex is SdkConnectionException or SdkTimeoutException)
            {
                if (isWrite && attempt < maxAttempts)
                {
                    _logger.LogWarning("Upvest {Operation}: connection failure; replaying with the same idempotency key.", operation);
                    continue;
                }

                _logger.LogWarning(ex, "Upvest {Operation}: could not reach the provider.", operation);
                throw new UpvestIntegrationException(
                    $"Could not reach the investment provider for '{operation}'.",
                    statusCode: null,
                    outcomeUnknown: isWrite,
                    ex);
            }
        }
    }

    private UpvestIntegrationException TranslateApiException<TError>(ApiException<TError> ex, string operation) where TError : ApiError
    {
        var status = (int)ex.StatusCode;

        // Log the operation and provider HTTP status only. The provider's typed error body (reachable via
        // TryGetNoContent) can echo the personal data from the request, so it is deliberately never logged —
        // the strict "personal details are never written to logs" rule wins over richer diagnostics.
        _logger.LogWarning("Upvest {Operation} failed (HTTP {Status}).", operation, status);

        return new UpvestIntegrationException($"The investment provider rejected '{operation}' (HTTP {status}).", ex.StatusCode, outcomeUnknown: false, ex);
    }
}
