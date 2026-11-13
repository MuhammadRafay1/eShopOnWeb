using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
using UpvestInvestmentApi.Requests.TopUps;
using UpvestInvestmentApi.Requests.Users;
using UpvestInvestmentApi.Requests.Valuations;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.Upvest;

/// <summary>
/// The concrete Upvest boundary. Builds each request record from the SDK map, reads responses through their union
/// accessors, converts money/weights via <see cref="UpvestAmounts"/>, and collapses the SDK exception family into
/// <see cref="UpvestProviderException"/>. Request bodies carry personal data on the user-create path; they are never
/// logged here, and the built-in SDK request-body logging is left off (see DI registration).
/// </summary>
public sealed class UpvestGateway : IUpvestGateway
{
    private const int PageSize = 100;
    private const int MaxPages = 200; // backstop so a list walk never runs unbounded

    private readonly UpvestInvestmentApiClient _client;
    private readonly ILogger<UpvestGateway> _logger;

    public UpvestGateway(UpvestInvestmentApiClient client, ILogger<UpvestGateway> logger)
    {
        _client = client;
        _logger = logger;
    }

    // ---- Flow 1 ---------------------------------------------------------------------------------

    public Task<UpvestUserResult> CreateUserAsync(InvestorSignupDetails d, Guid idempotencyKey, CancellationToken ct) =>
        ExecuteAsync("CreateUser", isWrite: true, async () =>
        {
            var body = new UserTolCreateRequest
            {
                FirstName = d.FirstName,
                LastName = d.LastName,
                Email = d.Email,
                BirthDate = d.BirthDate,
                BirthCity = NullIfBlank(d.BirthCity),
                BirthCountry = ResolveBirthCountry(d.BirthCountry),
                Nationalities = new[] { ResolveNationality(d.Nationality) },
                PhoneNumber = NullIfBlank(d.PhoneNumber),
                Address = new Address2
                {
                    AddressLine1 = d.AddressLine1,
                    Postcode = d.Postcode,
                    City = d.City,
                    Country = ResolveCountry(d.AddressCountry),
                },
                // FATCA is required. We derive US-taxpayer status from the declared tax country.
                Fatca = new Fatca
                {
                    Status = string.Equals(d.TaxCountry, "US", StringComparison.OrdinalIgnoreCase),
                    ConfirmedAt = DateTimeOffset.UtcNow,
                },
            };

            var resp = await _client.Users.CreateUser(
                new CreateUserRequest { IdempotencyKey = idempotencyKey, Body = body },
                cancellationToken: ct);

            // The response is an AnyOf(UserByol, UserTol); both arms carry id+status, and the lenient AnyOf may pick
            // either, so read the common fields from whichever matched.
            return resp.Match(
                onUserByol: u => new UpvestUserResult(u.Id, u.Status.Value),
                onUserTol: u => new UpvestUserResult(u.Id, u.Status.Value),
                otherwise: _ => throw Unexpected("CreateUser returned an unrecognised user shape."));
        }, ct);

    public Task<Guid> CreateAccountGroupAsync(Guid userId, Guid idempotencyKey, CancellationToken ct) =>
        ExecuteAsync("CreateAccountGroup", isWrite: true, async () =>
        {
            var resp = await _client.AccountGroups.CreateAccountGroup(
                new CreateAccountGroupRequest
                {
                    IdempotencyKey = idempotencyKey,
                    Body = new AccountGroupCreateUserRequest { UserId = userId, Type = Type13.Personal },
                },
                cancellationToken: ct);

            return resp.Match(
                onAccountGroup: g => g.Id,
                onBusinessAccountGroup: _ => throw Unexpected("CreateAccountGroup returned a business group."),
                otherwise: _ => throw Unexpected("CreateAccountGroup returned an unrecognised shape."));
        }, ct);

    public Task<UpvestAccountResult> CreateAccountAsync(Guid userId, Guid accountGroupId, Guid idempotencyKey, CancellationToken ct) =>
        ExecuteAsync("CreateAccount", isWrite: true, async () =>
        {
            var resp = await _client.AccountsApi.CreateAccount(
                new CreateAccountRequest
                {
                    IdempotencyKey = idempotencyKey,
                    Body = new AccountCreateUserRequest
                    {
                        UserId = userId,
                        AccountGroupId = accountGroupId,
                        Type = Type16.Portfolio, // a managed portfolio needs a PORTFOLIO account
                        Name = "Managed portfolio",
                    },
                },
                cancellationToken: ct);

            return resp.Match(
                onAccount: a => new UpvestAccountResult(a.Id, a.AccountGroupId, a.Status.Value),
                onBusinessAccount: _ => throw Unexpected("CreateAccount returned a business account."),
                otherwise: _ => throw Unexpected("CreateAccount returned an unrecognised shape."));
        }, ct);

    public Task<UpvestUserResult> GetUserAsync(Guid userId, CancellationToken ct) =>
        ExecuteAsync("RetrieveUser", isWrite: false, async () =>
        {
            var resp = await _client.Users.RetrieveUser(new RetrieveUserRequest { UserId = userId }, cancellationToken: ct);
            return resp.Match(
                onUserByol: u => new UpvestUserResult(u.Id, u.Status.Value),
                onUserTol: u => new UpvestUserResult(u.Id, u.Status.Value),
                otherwise: _ => throw Unexpected("RetrieveUser returned an unrecognised user shape."));
        }, ct);

    public Task<string?> GetPortfolioAccountStatusAsync(Guid userId, Guid accountId, CancellationToken ct) =>
        ExecuteAsync("ListUserAccounts", isWrite: false, async () =>
        {
            var resp = await _client.AccountsApi.ListUserAccounts(
                new ListUserAccountsRequest { UserId = userId, Limit = PageSize }, cancellationToken: ct);
            var account = resp.Data.FirstOrDefault(a => a.Id == accountId) ?? resp.Data.FirstOrDefault();
            return account?.Status.Value;
        }, ct);

    // ---- Flow 2 ---------------------------------------------------------------------------------

    public Task<UpvestAllocationResult> CreateAllocationAsync(string name, IReadOnlyList<FundWeight> funds, Guid idempotencyKey, CancellationToken ct) =>
        ExecuteAsync("CreatePortfoliosAllocation", isWrite: true, async () =>
        {
            var resp = await _client.Portfolios.CreatePortfoliosAllocation(
                new CreatePortfoliosAllocationRequest
                {
                    IdempotencyKey = idempotencyKey,
                    Body = new PortfoliosAllocationCreateRequest { Name = name, Allocation = ToAllocations(funds) },
                },
                cancellationToken: ct);
            return new UpvestAllocationResult(resp.Id);
        }, ct);

    public Task UpdateAllocationAsync(Guid allocationId, string name, IReadOnlyList<FundWeight> funds, CancellationToken ct) =>
        ExecuteAsync("UpdatePortfoliosAllocation", isWrite: true, async () =>
        {
            await _client.Portfolios.UpdatePortfoliosAllocation(
                new UpdatePortfoliosAllocationRequest
                {
                    AllocationId = allocationId,
                    Body = new PortfoliosAllocationUpdateRequest { Name = name, Allocation = ToAllocations(funds) },
                },
                cancellationToken: ct);
            return true;
        }, ct);

    public Task CreateConfigurationAsync(Guid accountId, Guid allocationId, Guid idempotencyKey, CancellationToken ct) =>
        ExecuteAsync("CreatePortfoliosConfiguration", isWrite: true, async () =>
        {
            await _client.Portfolios.CreatePortfoliosConfiguration(
                new CreatePortfoliosConfigurationRequest
                {
                    IdempotencyKey = idempotencyKey,
                    Body = new PortfoliosConfigurationCreateRequest { AccountId = accountId, AllocationId = allocationId },
                },
                cancellationToken: ct);
            return true;
        }, ct);

    public Task UpdateConfigurationAsync(Guid accountId, Guid allocationId, Guid idempotencyKey, CancellationToken ct) =>
        ExecuteAsync("UpdatePortfoliosConfiguration", isWrite: true, async () =>
        {
            await _client.Portfolios.UpdatePortfoliosConfiguration(
                new UpdatePortfoliosConfigurationRequest
                {
                    AccountId = accountId,
                    IdempotencyKey = idempotencyKey,
                    Body = new PortfoliosConfigurationUpdateRequest { AllocationId = allocationId },
                },
                cancellationToken: ct);
            return true;
        }, ct);

    public Task<Guid?> GetFollowedAllocationAsync(Guid accountId, CancellationToken ct) =>
        ExecuteAsync("RetrievePortfoliosConfiguration", isWrite: false, async () =>
        {
            var resp = await _client.Portfolios.RetrievePortfoliosConfiguration(
                new RetrievePortfoliosConfigurationRequest { AccountId = accountId }, cancellationToken: ct);
            return (Guid?)resp.AllocationId;
        }, ct);

    public Task<ISet<string>> ListTradableIsinsAsync(CancellationToken ct) =>
        ExecuteAsync("ListInstruments", isWrite: false, async () =>
        {
            var isins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int offset = 0;
            for (int page = 0; page < MaxPages; page++)
            {
                var resp = await _client.Instruments.ListInstruments(
                    new ListInstrumentsRequest { Offset = offset, Limit = PageSize }, cancellationToken: ct);
                foreach (var item in resp.Data)
                {
                    if (!string.IsNullOrWhiteSpace(item.Isin))
                        isins.Add(item.Isin!);
                }
                offset += resp.Meta.Count;
                if (resp.Meta.Count == 0 || offset >= resp.Meta.TotalCount)
                    break;
            }
            return (ISet<string>)isins;
        }, ct);

    // ---- Flow 3 ---------------------------------------------------------------------------------

    public Task<UpvestTopupResult> CreateTopupAsync(Guid accountGroupId, decimal amount, Guid idempotencyKey, CancellationToken ct) =>
        ExecuteAsync("CreateTopup", isWrite: true, async () =>
        {
            var resp = await _client.TopUps.CreateTopup(
                new CreateTopupRequest
                {
                    IdempotencyKey = idempotencyKey,
                    Body = new PaymentsTopUpCreateRequest
                    {
                        AccountGroupId = accountGroupId,
                        CashAmount = UpvestAmounts.ToCashAmount(amount),
                        Currency = Currency.Eur,
                    },
                },
                cancellationToken: ct);
            return new UpvestTopupResult(resp.Id, resp.Status?.Value, resp.SettlementReference);
        }, ct);

    public Task<UpvestOrderResult> CreateBuyOrderAsync(Guid userId, Guid accountId, decimal amount, Guid idempotencyKey, CancellationToken ct) =>
        ExecuteAsync("CreatePortfoliosOrder", isWrite: true, async () =>
        {
            var resp = await _client.Portfolios.CreatePortfoliosOrder(
                new CreatePortfoliosOrderRequest
                {
                    IdempotencyKey = idempotencyKey,
                    Body = new PortfoliosOrderPlaceRequest
                    {
                        UserId = userId,
                        AccountId = accountId,
                        CashAmount = UpvestAmounts.ToCashAmount(amount),
                        Currency = Currency.Eur,
                        Side = Side15.Buy,
                    },
                },
                cancellationToken: ct);
            return new UpvestOrderResult(resp.Id, resp.Status.Value);
        }, ct);

    public Task<string?> GetOrderStatusAsync(Guid orderId, CancellationToken ct) =>
        ExecuteAsync("RetrievePortfoliosOrder", isWrite: false, async () =>
        {
            var resp = await _client.Portfolios.RetrievePortfoliosOrder(
                new RetrievePortfoliosOrderRequest { PortfolioOrderId = orderId }, cancellationToken: ct);
            return (string?)resp.Status.Value;
        }, ct);

    // ---- Flow 4 ---------------------------------------------------------------------------------

    public Task<Guid> TriggerRealignmentAsync(Guid allocationId, Guid idempotencyKey, CancellationToken ct) =>
        ExecuteAsync("TriggerPortfolioRebalancing", isWrite: true, async () =>
        {
            var resp = await _client.PortfoliosRebalancing.TriggerPortfolioRebalancing(
                new TriggerPortfolioRebalancingOperationRequest
                {
                    IdempotencyKey = idempotencyKey,
                    Body = new Allocations { AllocationsValue = new[] { allocationId } },
                },
                cancellationToken: ct);
            return resp.Id;
        }, ct);

    public Task<string> GetRealignmentStatusAsync(Guid executionId, CancellationToken ct) =>
        ExecuteAsync("ListPortfolioRebalancingExecutionOrders", isWrite: false, async () =>
        {
            // The execution resource itself carries no status, so status is derived from its orders:
            //  any NEW/PROCESSING (or no orders yet) -> pending; else at least one FILLED -> completed; else failed.
            var orders = new List<string>();
            int offset = 0;
            for (int page = 0; page < MaxPages; page++)
            {
                var resp = await _client.PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders(
                    new ListPortfolioRebalancingExecutionOrdersRequest { ExecutionId = executionId, Offset = offset, Limit = PageSize },
                    cancellationToken: ct);
                orders.AddRange(resp.Data.Select(o => o.Status.Value));
                offset += resp.Meta.Count;
                if (resp.Meta.Count == 0 || offset >= resp.Meta.TotalCount)
                    break;
            }
            return DeriveRealignmentStatus(orders);
        }, ct);

    // ---- Flow 5 ---------------------------------------------------------------------------------

    public Task<UpvestValuationResult> GetValuationAsync(Guid accountId, CancellationToken ct) =>
        ExecuteAsync("GetAccountValuation", isWrite: false, async () =>
        {
            var resp = await _client.Valuations.GetAccountValuation(
                new GetAccountValuationRequest { AccountId = accountId, PriceQuality = PriceQuality3.HighestAvailable },
                cancellationToken: ct);

            var holdings = resp.SecurityPositions.Select(p => new UpvestHolding(
                InstrumentId: p.Instrument.Isin ?? p.Instrument.Uuid.ToString(),
                Quantity: UpvestAmounts.ParseDecimal(p.Quantity),
                Value: p.Value is null ? (decimal?)null : UpvestAmounts.ToMoney(UpvestAmounts.ParseDecimal(p.Value.Amount)))).ToList();

            var total = UpvestAmounts.ToMoney(UpvestAmounts.ParseDecimal(resp.TotalSecurityValue.Amount));
            return new UpvestValuationResult(total, holdings);
        }, ct);

    // ---- helpers --------------------------------------------------------------------------------

    private static string DeriveRealignmentStatus(IReadOnlyList<string> orderStatuses)
    {
        if (orderStatuses.Count == 0)
            return "pending";
        if (orderStatuses.Any(s => s is "NEW" or "PROCESSING"))
            return "pending";
        return orderStatuses.Any(s => s == "FILLED") ? "completed" : "failed";
    }

    private static IReadOnlyList<Allocation> ToAllocations(IReadOnlyList<FundWeight> funds) =>
        funds.Select(f => new Allocation
        {
            InstrumentId = InstrumentId.String(f.InstrumentId),
            InstrumentIdType = InstrumentIdType4.Isin,
            Weight = UpvestAmounts.ToWeight(f.Weight),
        }).ToList();

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static Nationality ResolveNationality(string code)
    {
        if (Nationality.TryGetKnownValue(code, out var n))
            return n;
        throw new UpvestProviderException($"Unknown nationality code '{code}'.", HttpStatusCode.BadRequest);
    }

    private static Country ResolveCountry(string code)
    {
        if (Country.TryGetKnownValue(code, out var c))
            return c;
        throw new UpvestProviderException($"Unknown country code '{code}'.", HttpStatusCode.BadRequest);
    }

    private static BirthCountry? ResolveBirthCountry(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;
        if (BirthCountry.TryGetKnownValue(code, out var c))
            return c;
        throw new UpvestProviderException($"Unknown birth country code '{code}'.", HttpStatusCode.BadRequest);
    }

    private static UpvestProviderException Unexpected(string message) =>
        new(message, HttpStatusCode.BadGateway);

    /// <summary>
    /// Runs one SDK call and translates every failure into <see cref="UpvestProviderException"/>. A validation
    /// <see cref="UpvestProviderException"/> thrown while building the request (unknown country, bad amount) passes
    /// through unchanged. On a transport failure on a write the outcome is marked unknown so the caller can reconcile.
    /// </summary>
    private async Task<T> ExecuteAsync<T>(string operation, bool isWrite, Func<Task<T>> call, CancellationToken ct)
    {
        try
        {
            return await call();
        }
        catch (UpvestProviderException)
        {
            throw; // request-building validation already in our terms
        }
        catch (ApiException ex) // covers ApiException<TError> and ResponseDeserializationException
        {
            var status = ex.StatusCode;
            var unauthorizedScope = status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
            if (unauthorizedScope)
                _logger.LogWarning("Upvest {Operation} refused ({Status}): client not authorised for the required scope.", operation, (int)status);
            else
                _logger.LogWarning("Upvest {Operation} returned {Status}.", operation, (int)status);
            throw new UpvestProviderException(
                $"Upvest {operation} returned status {(int)status}.", status, unauthorizedScope: unauthorizedScope, inner: ex);
        }
        catch (SdkTimeoutException ex)
        {
            _logger.LogWarning("Upvest {Operation} timed out.", operation);
            throw new UpvestProviderException($"Upvest {operation} did not answer in time.", null, outcomeUnknown: isWrite, inner: ex);
        }
        catch (SdkConnectionException ex)
        {
            _logger.LogWarning("Upvest {Operation} connection failed.", operation);
            throw new UpvestProviderException($"Upvest {operation} could not be reached.", null, outcomeUnknown: isWrite, inner: ex);
        }
        catch (AuthSchemeException ex)
        {
            // Upvest refuses an unauthorised scope at the TOKEN endpoint (POST /auth/token -> 401), which surfaces here
            // rather than as an operation 401. In this sandbox the client is authorised only for user operations, so a
            // token refused for another scope is the expected environment limitation — flag it as unauthorised scope so
            // callers degrade gracefully. (Missing credentials are caught at startup, before any call.)
            var tokenRefused = HasStatus(ex, HttpStatusCode.Unauthorized) || HasStatus(ex, HttpStatusCode.Forbidden);
            if (tokenRefused)
                _logger.LogWarning("Upvest {Operation}: token request refused for the required scope; client not authorised.", operation);
            else
                _logger.LogError(ex, "Upvest {Operation} failed: credentials missing or rejected.", operation);
            throw new UpvestProviderException(
                tokenRefused ? $"Upvest {operation} is not authorised for this client." : "Upvest credentials are missing or were rejected.",
                tokenRefused ? HttpStatusCode.Unauthorized : (HttpStatusCode?)null,
                unauthorizedScope: tokenRefused, inner: ex);
        }
    }

    private static bool HasStatus(Exception? ex, HttpStatusCode status)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is ApiException api && api.StatusCode == status)
                return true;
            if (e is AggregateException agg)
            {
                foreach (var inner in agg.InnerExceptions)
                    if (HasStatus(inner, status))
                        return true;
            }
        }
        return false;
    }
}
