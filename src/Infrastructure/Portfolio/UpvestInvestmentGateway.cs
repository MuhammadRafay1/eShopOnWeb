using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Exceptions;
using UpvestInvestmentApi.Models;
using UpvestInvestmentApi.Models.AnyOf;
using UpvestInvestmentApi.Models.Enums;
using UpvestInvestmentApi.Requests.AccountGroups;
using UpvestInvestmentApi.Requests.AccountsApi;
using UpvestInvestmentApi.Requests.Instruments;
using UpvestInvestmentApi.Requests.Portfolios;
using UpvestInvestmentApi.Requests.PortfoliosRebalancing;
using UpvestInvestmentApi.Requests.Positions;
using UpvestInvestmentApi.Requests.TaxResidencies;
using UpvestInvestmentApi.Requests.TopUps;
using UpvestInvestmentApi.Requests.UserChecks;
using UpvestInvestmentApi.Requests.UserIdentifiers;
using UpvestInvestmentApi.Requests.Users;
using UpvestInvestmentApi.Requests.Valuations;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio;

/// <summary>
/// The Upvest Investment API SDK boundary. Every public method is domain-shaped; SDK failures are
/// translated to <see cref="UpvestGatewayException"/> in one place. No personal data is logged.
/// </summary>
public class UpvestInvestmentGateway : IUpvestInvestmentGateway
{
    private readonly UpvestInvestmentApiClient _client;
    private readonly IAppLogger<UpvestInvestmentGateway> _logger;

    public UpvestInvestmentGateway(UpvestInvestmentApiClient client, IAppLogger<UpvestInvestmentGateway> logger)
    {
        _client = client;
        _logger = logger;
    }

    // --- Flow 1: onboarding -------------------------------------------------------------------

    public async Task<Guid> CreateUserAsync(InvestorRegistration registration, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var body = new UserTolCreateRequest
        {
            FirstName = registration.FirstName,
            LastName = registration.LastName,
            Email = registration.Email,
            BirthDate = ToDateTimeOffset(registration.BirthDate),
            BirthCity = registration.BirthCity,
            BirthCountry = ResolveOptional<BirthCountry>(registration.BirthCountry, BirthCountry.TryGetKnownValue),
            Nationalities = new[] { ResolveRequired<Nationality>(registration.Nationality, Nationality.TryGetKnownValue, "nationality") },
            PhoneNumber = registration.PhoneNumber,
            Address = new Address2
            {
                AddressLine1 = registration.Address.Line1,
                Postcode = registration.Address.Postcode,
                City = registration.Address.City,
                Country = ResolveRequired<Country>(registration.Address.Country, Country.TryGetKnownValue, "address.country")
            },
            Fatca = new Fatca { Status = false, ConfirmedAt = DateTimeOffset.UtcNow }
        };

        var request = new CreateUserRequest
        {
            IdempotencyKey = idempotencyKey,
            Body = UserCreateRequest.UserTolCreateRequest(body)
        };

        var response = await ExecuteAsync(ct => _client.Users.CreateUser(request, cancellationToken: ct), isWrite: true, "CreateUser", cancellationToken);
        if (response.TryGetUserTol(out var user))
        {
            return user.Id;
        }
        throw new UpvestGatewayException("Upvest returned an unexpected shape for the created user.");
    }

    public async Task CreateIdentifierAsync(Guid userId, RegistrationIdentityDocument document, CancellationToken cancellationToken)
    {
        var body = new IdentifierCreateRequest
        {
            Type = string.Equals(document.Type?.Trim(), "PASSPORT", StringComparison.OrdinalIgnoreCase) ? Type7.Passport : Type7.NationalId,
            IssuingCountry = ResolveRequired<IssuingCountry>(document.IssuingCountry, IssuingCountry.TryGetKnownValue, "identityDocument.issuingCountry"),
            Identifier = document.Number
        };

        var request = new CreateIdentifierRequest { UserId = userId, Body = body };
        await ExecuteAsync(ct => _client.UserIdentifiers.CreateIdentifier(request, cancellationToken: ct), isWrite: true, "CreateIdentifier", cancellationToken);
    }

    public async Task SetTaxResidenciesAsync(Guid userId, string taxId, string taxCountry, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var residency = new WithTaxIdentifierNumber
        {
            Country = ResolveRequired<Country>(taxCountry, Country.TryGetKnownValue, "taxCountry"),
            TaxIdentifierNumber = taxId
        };
        var body = new TaxResidenciesSetRequest
        {
            TaxResidencies = new[] { TaxResidencyForCreateRequest.WithTaxIdentifierNumber(residency) }
        };
        var request = new SetTaxResidenciesRequest { UserId = userId, IdempotencyKey = idempotencyKey, Body = body };
        await ExecuteAsync(ct => _client.TaxResidencies.SetTaxResidencies(request, cancellationToken: ct), isWrite: true, "SetTaxResidencies", cancellationToken);
    }

    public async Task CreateUserCheckAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Record that the shop verified the shopper (TOL model). The shop performs KYC externally;
        // app-level defaults stand in for the externally-sourced provider/method/link.
        var kyc = new UserCheckKnowYourCustomerCreateRequest
        {
            CheckConfirmedAt = DateTimeOffset.UtcNow,
            DataDownloadLink = $"https://eshoponweb.invalid/kyc/{userId}",
            DocumentType = DocumentType3.IdCard,
            Provider = "eShopOnWeb",
            Method = Method.InPersonId
        };
        var request = new CreateUserCheckRequest { UserId = userId, Body = UserCheckCreateRequest.UserCheckKnowYourCustomerCreateRequest(kyc) };
        await ExecuteAsync(ct => _client.UserChecks.CreateUserCheck(request, cancellationToken: ct), isWrite: true, "CreateUserCheck", cancellationToken);
    }

    public async Task<Guid> CreateAccountGroupAsync(Guid userId, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var body = new AccountGroupCreateUserRequest { UserId = userId, Type = Type13.Personal };
        var request = new CreateAccountGroupRequest { IdempotencyKey = idempotencyKey, Body = AccountGroupCreateRequest.AccountGroupCreateUserRequest(body) };
        var response = await ExecuteAsync(ct => _client.AccountGroups.CreateAccountGroup(request, cancellationToken: ct), isWrite: true, "CreateAccountGroup", cancellationToken);
        if (response.TryGetAccountGroup(out var group))
        {
            return group.Id;
        }
        throw new UpvestGatewayException("Upvest returned an unexpected shape for the created account group.");
    }

    public async Task<Guid> CreateAccountAsync(Guid userId, Guid accountGroupId, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var body = new AccountCreateUserRequest
        {
            UserId = userId,
            AccountGroupId = accountGroupId,
            Type = Type16.Portfolio,
            Name = "Managed portfolio"
        };
        var request = new CreateAccountRequest { IdempotencyKey = idempotencyKey, Body = AccountCreateRequest.AccountCreateUserRequest(body) };
        var response = await ExecuteAsync(ct => _client.AccountsApi.CreateAccount(request, cancellationToken: ct), isWrite: true, "CreateAccount", cancellationToken);
        if (response.TryGetAccount(out var account))
        {
            return account.Id;
        }
        throw new UpvestGatewayException("Upvest returned an unexpected shape for the created account.");
    }

    public async Task<UpvestUserStatus> GetUserStatusAsync(Guid userId, CancellationToken cancellationToken)
    {
        var request = new RetrieveUserRequest { UserId = userId };
        var response = await ExecuteAsync(ct => _client.Users.RetrieveUser(request, cancellationToken: ct), isWrite: false, "RetrieveUser", cancellationToken);
        if (response.TryGetUserTol(out var user))
        {
            return user.Status == Status.Active ? UpvestUserStatus.Active : UpvestUserStatus.NotActive;
        }
        return UpvestUserStatus.Unknown;
    }

    public async Task<UpvestAccountStatus> GetAccountStatusAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var request = new RetrieveAccountRequest { AccountId = accountId };
        var response = await ExecuteAsync(ct => _client.AccountsApi.RetrieveAccount(request, cancellationToken: ct), isWrite: false, "RetrieveAccount", cancellationToken);
        if (response.TryGetAccount(out var account))
        {
            if (account.Status == Status21.Active) return UpvestAccountStatus.Active;
            if (account.Status == Status21.PendingApproval) return UpvestAccountStatus.Pending;
            return UpvestAccountStatus.Closed;
        }
        return UpvestAccountStatus.Unknown;
    }

    // --- Flow 2: model portfolios -------------------------------------------------------------

    public async Task<Guid> CreateAllocationAsync(string name, IReadOnlyList<FundWeight> funds, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var body = new PortfoliosAllocationCreateRequest { Name = name, Allocation = ToAllocation(funds) };
        var request = new CreatePortfoliosAllocationRequest { IdempotencyKey = idempotencyKey, Body = body };
        var response = await ExecuteAsync(ct => _client.Portfolios.CreatePortfoliosAllocation(request, cancellationToken: ct), isWrite: true, "CreatePortfoliosAllocation", cancellationToken);
        return response.Id;
    }

    public async Task UpdateAllocationAsync(Guid allocationId, string name, IReadOnlyList<FundWeight> funds, CancellationToken cancellationToken)
    {
        var body = new PortfoliosAllocationUpdateRequest { Name = name, Allocation = ToAllocation(funds) };
        var request = new UpdatePortfoliosAllocationRequest { AllocationId = allocationId, Body = body };
        await ExecuteAsync(ct => _client.Portfolios.UpdatePortfoliosAllocation(request, cancellationToken: ct), isWrite: true, "UpdatePortfoliosAllocation", cancellationToken);
    }

    public async Task<IReadOnlyList<AllocationSummary>> ListAllocationsAsync(CancellationToken cancellationToken)
    {
        var result = new List<AllocationSummary>();
        int offset = 0;
        const int limit = 100;
        const int maxPages = 100;

        for (int page = 0; page < maxPages; page++)
        {
            var request = new ListPortfoliosAllocationsRequest { Offset = offset, Limit = limit };
            var response = await ExecuteAsync(ct => _client.Portfolios.ListPortfoliosAllocations(request, cancellationToken: ct), isWrite: false, "ListPortfoliosAllocations", cancellationToken);

            result.AddRange(response.Data.Select(a => new AllocationSummary(
                a.Id,
                a.Name,
                a.Allocation.Select(x => new FundWeight(ReadInstrumentId(x.InstrumentId), ParseDecimal(x.Weight))).ToList())));

            if (response.Meta.Offset + response.Meta.Count >= response.Meta.TotalCount || response.Data.Count == 0)
            {
                break;
            }
            offset = response.Meta.Offset + response.Meta.Count;
        }

        return result;
    }

    public async Task<bool> InstrumentExistsAsync(string isin, CancellationToken cancellationToken)
    {
        var request = new RetrieveInstrumentRequest { InstrumentId = InstrumentUniqueIdentifier.String($"isin:{isin}") };
        try
        {
            await ExecuteAsync(ct => _client.Instruments.RetrieveInstrument(request, cancellationToken: ct), isWrite: false, "RetrieveInstrument", cancellationToken);
            return true;
        }
        catch (UpvestGatewayException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task CreateConfigurationAsync(Guid accountId, Guid allocationId, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var body = new PortfoliosConfigurationCreateRequest { AccountId = accountId, AllocationId = allocationId };
        var request = new CreatePortfoliosConfigurationRequest { IdempotencyKey = idempotencyKey, Body = body };
        await ExecuteAsync(ct => _client.Portfolios.CreatePortfoliosConfiguration(request, cancellationToken: ct), isWrite: true, "CreatePortfoliosConfiguration", cancellationToken);
    }

    public async Task UpdateConfigurationAsync(Guid accountId, Guid allocationId, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var body = new PortfoliosConfigurationUpdateRequest { AllocationId = allocationId };
        var request = new UpdatePortfoliosConfigurationRequest { AccountId = accountId, IdempotencyKey = idempotencyKey, Body = body };
        await ExecuteAsync(ct => _client.Portfolios.UpdatePortfoliosConfiguration(request, cancellationToken: ct), isWrite: true, "UpdatePortfoliosConfiguration", cancellationToken);
    }

    public async Task<Guid?> GetConfiguredAllocationAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var request = new RetrievePortfoliosConfigurationRequest { AccountId = accountId };
        try
        {
            var response = await ExecuteAsync(ct => _client.Portfolios.RetrievePortfoliosConfiguration(request, cancellationToken: ct), isWrite: false, "RetrievePortfoliosConfiguration", cancellationToken);
            return response.AllocationId;
        }
        catch (UpvestGatewayException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    // --- Flow 3: paying in --------------------------------------------------------------------

    public async Task<Guid> CreateTopupAsync(Guid accountGroupId, decimal amount, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var body = new PaymentsTopUpCreateRequest
        {
            AccountGroupId = accountGroupId,
            CashAmount = FormatMoney(amount),
            Currency = Currency.Eur
        };
        var request = new CreateTopupRequest { IdempotencyKey = idempotencyKey, Body = body };
        var response = await ExecuteAsync(ct => _client.TopUps.CreateTopup(request, cancellationToken: ct), isWrite: true, "CreateTopup", cancellationToken);
        return response.Id;
    }

    public async Task<UpvestLegStatus> GetTopupStatusAsync(Guid topupId, CancellationToken cancellationToken)
    {
        var request = new RetrieveTopupRequest { TopupId = topupId };
        var response = await ExecuteAsync(ct => _client.TopUps.RetrieveTopup(request, cancellationToken: ct), isWrite: false, "RetrieveTopup", cancellationToken);
        if (response.Status == Status29.Cancelled) return UpvestLegStatus.Failed;
        if (response.Status == Status29.Settled || response.Status == Status29.Confirmed) return UpvestLegStatus.Settled;
        return UpvestLegStatus.Pending;
    }

    public async Task<Guid> CreateOrderAsync(Guid userId, Guid accountId, decimal amount, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var body = new PortfoliosOrderPlaceRequest
        {
            UserId = userId,
            AccountId = accountId,
            CashAmount = FormatMoney(amount),
            Currency = Currency.Eur,
            Side = Side15.Buy
        };
        var request = new CreatePortfoliosOrderRequest { IdempotencyKey = idempotencyKey, Body = body };
        var response = await ExecuteAsync(ct => _client.Portfolios.CreatePortfoliosOrder(request, cancellationToken: ct), isWrite: true, "CreatePortfoliosOrder", cancellationToken);
        return response.Id;
    }

    public async Task<UpvestLegStatus> GetOrderStatusAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var request = new RetrievePortfoliosOrderRequest { PortfolioOrderId = orderId };
        var response = await ExecuteAsync(ct => _client.Portfolios.RetrievePortfoliosOrder(request, cancellationToken: ct), isWrite: false, "RetrievePortfoliosOrder", cancellationToken);
        if (response.Status == Status65.Cancelled) return UpvestLegStatus.Failed;
        if (response.Status == Status65.Filled || response.Status == Status65.Settled) return UpvestLegStatus.Settled;
        return UpvestLegStatus.Pending;
    }

    // --- Flow 4: realignment ------------------------------------------------------------------

    public async Task<Guid> TriggerRebalancingAsync(Guid allocationId, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var body = TriggerPortfolioRebalancingRequest.Allocations(new Allocations { AllocationsValue = new[] { allocationId } });
        var request = new TriggerPortfolioRebalancingOperationRequest { IdempotencyKey = idempotencyKey, Body = body };
        var response = await ExecuteAsync(ct => _client.PortfoliosRebalancing.TriggerPortfolioRebalancing(request, cancellationToken: ct), isWrite: true, "TriggerPortfolioRebalancing", cancellationToken);
        return response.Id;
    }

    public async Task<UpvestRealignmentStatus> GetRebalancingStatusAsync(Guid executionId, CancellationToken cancellationToken)
    {
        // The execution resource carries no status; derive it from the per-order statuses, paging
        // all orders so the derived status is not computed from a truncated page.
        var statuses = new List<Status71>();
        int offset = 0;
        const int limit = 100;
        const int maxPages = 100;

        for (int page = 0; page < maxPages; page++)
        {
            var request = new ListPortfolioRebalancingExecutionOrdersRequest { ExecutionId = executionId, Offset = offset, Limit = limit };
            var response = await ExecuteAsync(ct => _client.PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders(request, cancellationToken: ct), isWrite: false, "ListPortfolioRebalancingExecutionOrders", cancellationToken);
            statuses.AddRange(response.Data.Select(o => o.Status));

            if (response.Meta.Offset + response.Meta.Count >= response.Meta.TotalCount || response.Data.Count == 0)
            {
                break;
            }
            offset = response.Meta.Offset + response.Meta.Count;
        }

        if (statuses.Count == 0)
        {
            return UpvestRealignmentStatus.Pending;
        }
        if (statuses.Any(s => s == Status71.Cancelled))
        {
            return UpvestRealignmentStatus.Failed;
        }
        if (statuses.All(s => s == Status71.Filled))
        {
            return UpvestRealignmentStatus.Completed;
        }
        return UpvestRealignmentStatus.Pending;
    }

    // --- Flow 5: the investor's portfolio -----------------------------------------------------

    public async Task<IReadOnlyList<Position>> GetPositionsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var result = new List<Position>();
        int offset = 0;
        const int limit = 100;
        const int maxPages = 100;

        for (int page = 0; page < maxPages; page++)
        {
            var request = new ListPositionsRequest { AccountId = accountId, Offset = offset, Limit = limit };
            var response = await ExecuteAsync(ct => _client.Positions.ListPositions(request, cancellationToken: ct), isWrite: false, "ListPositions", cancellationToken);

            result.AddRange(response.Data
                .Where(d => d.Instrument.Isin is not null)
                .Select(d => new Position(d.Instrument.Isin!, ParseDecimal(d.Quantity))));

            if (response.Meta.Offset + response.Meta.Count >= response.Meta.TotalCount || response.Data.Count == 0)
            {
                break;
            }
            offset = response.Meta.Offset + response.Meta.Count;
        }

        return result;
    }

    public async Task<PortfolioValuation> GetValuationAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var request = new GetAccountValuationRequest { AccountId = accountId, PriceQuality = PriceQuality3.HighestAvailable };
        var response = await ExecuteAsync(ct => _client.Valuations.GetAccountValuation(request, cancellationToken: ct), isWrite: false, "GetAccountValuation", cancellationToken);

        var total = ParseDecimal(response.TotalSecurityValue.Amount);
        var currency = response.TotalSecurityValue.Currency.Value;
        var perInstrument = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var sp in response.SecurityPositions)
        {
            if (sp.Instrument.Isin is { } isin && sp.Value is { } v)
            {
                perInstrument[isin] = ParseDecimal(v.Amount);
            }
        }
        return new PortfolioValuation(total, currency, perInstrument);
    }

    // --- helpers ------------------------------------------------------------------------------

    private static IReadOnlyList<Allocation> ToAllocation(IReadOnlyList<FundWeight> funds) =>
        funds.Select(f => new Allocation
        {
            InstrumentId = InstrumentId.String(f.InstrumentId),
            InstrumentIdType = InstrumentIdType4.Isin,
            Weight = FormatWeight(f.Weight)
        }).ToList();

    private static string ReadInstrumentId(InstrumentId id) =>
        id.TryGetString(out var s) ? s : (id.TryGetGuid(out var g) ? g.ToString() : string.Empty);

    private static DateTimeOffset ToDateTimeOffset(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static string FormatMoney(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.ToZero).ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatWeight(decimal weight) =>
        weight.ToString("0.#####", CultureInfo.InvariantCulture);

    private static decimal ParseDecimal(string value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;

    private delegate bool TryResolve<T>(string value, out T result);

    private static T ResolveRequired<T>(string code, TryResolve<T> tryResolve, string field)
    {
        if (!string.IsNullOrWhiteSpace(code) && tryResolve(code.Trim().ToUpperInvariant(), out var value))
        {
            return value;
        }
        throw new ArgumentException($"'{code}' is not a valid value for {field}.", field);
    }

    private static T? ResolveOptional<T>(string? code, TryResolve<T> tryResolve) where T : class =>
        !string.IsNullOrWhiteSpace(code) && tryResolve(code.Trim().ToUpperInvariant(), out var value) ? value : null;

    private async Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> call, bool isWrite, string operation, CancellationToken cancellationToken)
    {
        try
        {
            return await call(cancellationToken);
        }
        catch (ResponseDeserializationException ex)
        {
            var unknown = isWrite && IsSuccess(ex.StatusCode);
            _logger.LogWarning("Upvest {Operation} returned an unreadable body (status={Status}).", operation, (int)ex.StatusCode);
            throw new UpvestGatewayException($"Upvest {operation} returned a response that could not be processed.", ex.StatusCode, unknown, ex);
        }
        catch (ApiException ex)
        {
            var scopeWall = ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
            _logger.LogWarning("Upvest {Operation} failed (status={Status}).", operation, (int)ex.StatusCode);
            throw new UpvestGatewayException($"Upvest {operation} failed with status {(int)ex.StatusCode}.", ex.StatusCode, innerException: ex) { IsScopeWall = scopeWall };
        }
        catch (SdkTimeoutException ex)
        {
            _logger.LogWarning("Upvest {Operation} timed out.", operation);
            throw new UpvestGatewayException($"Upvest {operation} did not answer in time.", outcomeUnknown: isWrite, innerException: ex);
        }
        catch (SdkConnectionException ex)
        {
            _logger.LogWarning("Upvest {Operation} connection failed.", operation);
            throw new UpvestGatewayException($"Upvest {operation} could not be reached.", outcomeUnknown: isWrite, innerException: ex);
        }
        catch (AuthSchemeException ex)
        {
            _logger.LogWarning("Upvest {Operation} could not authenticate.", operation);
            throw new UpvestGatewayException($"Upvest {operation} could not authenticate the client.", innerException: ex);
        }
    }

    private async Task ExecuteAsync(Func<CancellationToken, Task> call, bool isWrite, string operation, CancellationToken cancellationToken) =>
        await ExecuteAsync<object?>(async ct => { await call(ct); return null; }, isWrite, operation, cancellationToken);

    private static bool IsSuccess(HttpStatusCode status) => (int)status is >= 200 and < 300;
}
