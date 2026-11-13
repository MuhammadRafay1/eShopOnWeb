using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
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
using UpvestInvestmentApi.Requests.UserChecks;
using UpvestInvestmentApi.Requests.Users;
using UpvestInvestmentApi.Requests.Valuations;
using UpvestInvestmentApi.Requests.VirtualCashBalances;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The Upvest adapter. Builds every request from grounded SDK contracts and translates all SDK
/// failures into <see cref="UpvestGatewayException"/>. It holds no state and logs no personal data.
/// </summary>
public sealed class UpvestPortfolioGateway : IUpvestPortfolioGateway
{
    // Page cap for the self-driven offset/limit reads, so a mis-behaving provider cannot spin forever.
    private const int MaxPages = 100;
    private const int PageSize = 100;

    private readonly UpvestInvestmentApiClient _client;
    private readonly IAppLogger<UpvestPortfolioGateway> _logger;

    public UpvestPortfolioGateway(UpvestInvestmentApiClient client, IAppLogger<UpvestPortfolioGateway> logger)
    {
        _client = client;
        _logger = logger;
    }

    // ----------------------------------------------------------------- Flow 1

    public Task<Guid> CreateUserAsync(InvestorRegistration r, Guid idempotencyKey, CancellationToken ct) =>
        CallAsync("create user", isWrite: true, ct, async () =>
        {
            var body = new UserTolCreateRequest
            {
                FirstName = r.FirstName,
                LastName = r.LastName,
                Email = r.Email,
                BirthDate = ParseDate(r.BirthDate, "birthDate"),
                BirthCity = r.BirthCity,
                BirthCountry = ResolveOptional<BirthCountry>(r.BirthCountry),
                Nationalities = new[] { Resolve<Nationality>(r.Nationality, "nationality") },
                PhoneNumber = string.IsNullOrWhiteSpace(r.PhoneNumber) ? null : r.PhoneNumber,
                Address = new Address2
                {
                    AddressLine1 = r.Address.Line1,
                    Postcode = r.Address.Postcode,
                    City = r.Address.City,
                    Country = Resolve<Country>(r.Address.Country, "address.country")
                },
                // The shopper confirmed their FATCA (US tax) status during sign-up.
                Fatca = new Fatca { Status = false, ConfirmedAt = DateTimeOffset.UtcNow }
            };

            var response = await _client.Users.CreateUser(
                new CreateUserRequest { IdempotencyKey = idempotencyKey, Body = UserCreateRequest.UserTolCreateRequest(body) },
                cancellationToken: ct);

            if (response.TryGetUserTol(out var user))
                return user.Id;
            if (response.TryGetUserByol(out var byol))
                return byol.Id;
            throw Unreadable("create user");
        });

    public Task<Guid> CreateAccountGroupAsync(Guid upvestUserId, Guid idempotencyKey, CancellationToken ct) =>
        CallAsync("create account group", isWrite: true, ct, async () =>
        {
            var body = new AccountGroupCreateUserRequest { UserId = upvestUserId, Type = Type13.Personal };

            var response = await _client.AccountGroups.CreateAccountGroup(
                new CreateAccountGroupRequest { IdempotencyKey = idempotencyKey, Body = AccountGroupCreateRequest.AccountGroupCreateUserRequest(body) },
                cancellationToken: ct);

            if (response.TryGetAccountGroup(out var group))
                return group.Id;
            if (response.TryGetBusinessAccountGroup(out var biz))
                return biz.Id;
            throw Unreadable("create account group");
        });

    public Task<AccountProvisioned> CreateAccountAsync(Guid upvestUserId, Guid accountGroupId, Guid idempotencyKey, CancellationToken ct) =>
        CallAsync("create account", isWrite: true, ct, async () =>
        {
            var body = new AccountCreateUserRequest
            {
                UserId = upvestUserId,
                AccountGroupId = accountGroupId,
                Type = Type16.Portfolio
            };

            var response = await _client.AccountsApi.CreateAccount(
                new CreateAccountRequest { IdempotencyKey = idempotencyKey, Body = AccountCreateRequest.AccountCreateUserRequest(body) },
                cancellationToken: ct);

            if (response.TryGetAccount(out var account))
                return new AccountProvisioned(account.Id, account.Status == Status21.Active);
            throw Unreadable("create account");
        });

    public Task<Guid> CreateKycCheckAsync(Guid upvestUserId, IdentityDocument document, string nationality, CancellationToken ct) =>
        CallAsync("create KYC check", isWrite: true, ct, async () =>
        {
            var body = new UserCheckKnowYourCustomerCreateRequest
            {
                CheckConfirmedAt = DateTimeOffset.UtcNow,
                // The shop performed the identity check; this records that confirmation at Upvest.
                DataDownloadLink = "https://eshoponweb.example/kyc-evidence",
                Provider = "eShopOnWeb",
                Method = Method.ElectronicId,
                DocumentType = ResolveDocumentType(document.Type),
                DocumentExpirationDate = ParseDate(document.ExpiryDate, "identityDocument.expiryDate"),
                Nationality = nationality
            };

            var response = await _client.UserChecks.CreateUserCheck(
                new CreateUserCheckRequest
                {
                    UserId = upvestUserId,
                    Body = UserCheckCreateRequest.UserCheckKnowYourCustomerCreateRequest(body)
                },
                cancellationToken: ct);

            return response.Id;
        });

    public Task<InvestorAcceptance> GetAcceptanceAsync(Guid upvestUserId, Guid accountId, CancellationToken ct) =>
        CallAsync("read investor acceptance", isWrite: false, ct, async () =>
        {
            var kyc = await _client.UserChecks.ListUserChecks(
                new ListUserChecksRequest { UserId = upvestUserId }, cancellationToken: ct);

            bool kycPassed = false, kycFailed = false;
            foreach (var check in kyc.Data)
            {
                if (check.TryGetUserCheckKnowYourCustomer(out var k))
                {
                    if (k.Status == Status6.Passed) kycPassed = true;
                    else if (k.Status == Status6.Failed) kycFailed = true;
                }
            }

            var accountResponse = await _client.AccountsApi.RetrieveAccount(
                new RetrieveAccountRequest { AccountId = accountId }, cancellationToken: ct);
            bool accountActive = accountResponse.TryGetAccount(out var account) && account.Status == Status21.Active;

            var userResponse = await _client.Users.RetrieveUser(
                new RetrieveUserRequest { UserId = upvestUserId }, cancellationToken: ct);
            bool offboarded = userResponse.TryGetUserTol(out var tol)
                && (tol.Status == Status.Offboarded || tol.Status == Status.Offboarding);

            return new InvestorAcceptance(kycPassed, kycFailed, accountActive, offboarded);
        });

    // ----------------------------------------------------------------- Flow 2

    public async Task EnsureInstrumentsExistAsync(IReadOnlyCollection<string> isins, CancellationToken ct)
    {
        foreach (var isin in isins)
        {
            try
            {
                await CallAsync("retrieve instrument", isWrite: false, ct, async () =>
                {
                    await _client.Instruments.RetrieveInstrument(
                        new RetrieveInstrumentRequest { InstrumentId = InstrumentUniqueIdentifier.String(isin) },
                        cancellationToken: ct);
                    return true;
                });
            }
            catch (UpvestGatewayException ex) when (ex.StatusCode == (int)HttpStatusCode.NotFound)
            {
                throw new PortfolioValidationException($"'{isin}' is not an instrument Upvest offers.");
            }
        }
    }

    public Task<Guid> CreateModelAllocationAsync(string name, IReadOnlyList<ModelFund> funds, Guid idempotencyKey, CancellationToken ct) =>
        CallAsync("create allocation", isWrite: true, ct, async () =>
        {
            var body = new PortfoliosAllocationCreateRequest { Name = name, Allocation = ToAllocations(funds) };
            var allocation = await _client.Portfolios.CreatePortfoliosAllocation(
                new CreatePortfoliosAllocationRequest { IdempotencyKey = idempotencyKey, Body = body },
                cancellationToken: ct);
            return allocation.Id;
        });

    public Task UpdateModelAllocationAsync(Guid allocationId, string name, IReadOnlyList<ModelFund> funds, CancellationToken ct) =>
        CallAsync("update allocation", isWrite: true, ct, async () =>
        {
            var body = new PortfoliosAllocationUpdateRequest { Name = name, Allocation = ToAllocations(funds) };
            await _client.Portfolios.UpdatePortfoliosAllocation(
                new UpdatePortfoliosAllocationRequest { AllocationId = allocationId, Body = body },
                cancellationToken: ct);
            return true;
        });

    public Task CreateInvestorConfigurationAsync(Guid accountId, Guid allocationId, Guid idempotencyKey, CancellationToken ct) =>
        CallAsync("create portfolio configuration", isWrite: true, ct, async () =>
        {
            await _client.Portfolios.CreatePortfoliosConfiguration(
                new CreatePortfoliosConfigurationRequest
                {
                    IdempotencyKey = idempotencyKey,
                    Body = new PortfoliosConfigurationCreateRequest { AccountId = accountId, AllocationId = allocationId }
                },
                cancellationToken: ct);
            return true;
        });

    public Task UpdateInvestorConfigurationAsync(Guid accountId, Guid allocationId, Guid idempotencyKey, CancellationToken ct) =>
        CallAsync("update portfolio configuration", isWrite: true, ct, async () =>
        {
            await _client.Portfolios.UpdatePortfoliosConfiguration(
                new UpdatePortfoliosConfigurationRequest
                {
                    AccountId = accountId,
                    IdempotencyKey = idempotencyKey,
                    Body = new PortfoliosConfigurationUpdateRequest { AllocationId = allocationId }
                },
                cancellationToken: ct);
            return true;
        });

    // ----------------------------------------------------------------- Flow 3

    public Task CreditCashAsync(Guid accountGroupId, decimal amount, Guid idempotencyKey, CancellationToken ct) =>
        CallAsync("credit cash", isWrite: true, ct, async () =>
        {
            await _client.VirtualCashBalances.CreateVirtualCashIncrease(
                new CreateVirtualCashIncreaseRequest
                {
                    IdempotencyKey = idempotencyKey,
                    Body = new VirtualCashBalanceVirtualCashIncreaseCreateRequest
                    {
                        AccountGroupId = accountGroupId,
                        Amount = FormatAmount(amount),
                        Currency = Currency1.Eur
                    }
                },
                cancellationToken: ct);
            return true;
        });

    public Task<Guid> PlaceOrderAsync(Guid upvestUserId, Guid accountId, decimal amount, Guid idempotencyKey, CancellationToken ct) =>
        CallAsync("place order", isWrite: true, ct, async () =>
        {
            var order = await _client.Portfolios.CreatePortfoliosOrder(
                new CreatePortfoliosOrderRequest
                {
                    IdempotencyKey = idempotencyKey,
                    Body = new PortfoliosOrderPlaceRequest
                    {
                        UserId = upvestUserId,
                        AccountId = accountId,
                        CashAmount = FormatAmount(amount),
                        Currency = Currency.Eur,
                        Side = Side15.Buy
                    }
                },
                cancellationToken: ct);
            return order.Id;
        });

    public Task<ProviderOutcome> GetOrderOutcomeAsync(Guid orderId, CancellationToken ct) =>
        CallAsync("read order", isWrite: false, ct, async () =>
        {
            var order = await _client.Portfolios.RetrievePortfoliosOrder(
                new RetrievePortfoliosOrderRequest { PortfolioOrderId = orderId }, cancellationToken: ct);
            return MapOrderStatus(order.Status);
        });

    // ----------------------------------------------------------------- Flow 4

    public Task<Guid> TriggerRealignmentAsync(Guid allocationId, Guid idempotencyKey, CancellationToken ct) =>
        CallAsync("trigger realignment", isWrite: true, ct, async () =>
        {
            var body = TriggerPortfolioRebalancingRequest.Allocations(
                new Allocations { AllocationsValue = new[] { allocationId } });

            var response = await _client.PortfoliosRebalancing.TriggerPortfolioRebalancing(
                new TriggerPortfolioRebalancingOperationRequest { IdempotencyKey = idempotencyKey, Body = body },
                cancellationToken: ct);
            return response.Id;
        });

    public Task<ProviderOutcome> GetRealignmentOutcomeAsync(Guid executionId, CancellationToken ct) =>
        CallAsync("read realignment", isWrite: false, ct, async () =>
        {
            // The rebalancing execution itself carries no status field, so the outcome is derived
            // from the statuses of the orders the execution produced (paged in full).
            // Confirm the execution exists first.
            await _client.PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution(
                new RetrievePortfoliosRebalancingExecutionRequest { ExecutionId = executionId }, cancellationToken: ct);

            var statuses = new List<Status71>();
            for (int page = 0, offset = 0; page < MaxPages; page++, offset += PageSize)
            {
                var ordersPage = await _client.PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders(
                    new ListPortfolioRebalancingExecutionOrdersRequest { ExecutionId = executionId, Offset = offset, Limit = PageSize },
                    cancellationToken: ct);

                var (items, meta) = ReadRebalancingOrders(ordersPage);
                statuses.AddRange(items);

                if (meta is null || offset + meta.Count >= meta.TotalCount || meta.Count == 0)
                    break;
            }

            return DeriveRealignmentOutcome(statuses);
        });

    // ----------------------------------------------------------------- Flow 5

    public Task<PortfolioSnapshot> GetPortfolioAsync(Guid accountId, CancellationToken ct) =>
        CallAsync("read portfolio", isWrite: false, ct, async () =>
        {
            // Authoritative quantities from positions (paged in full)...
            var quantities = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            for (int page = 0, offset = 0; page < MaxPages; page++, offset += PageSize)
            {
                var positions = await _client.Positions.ListPositions(
                    new ListPositionsRequest { AccountId = accountId, Offset = offset, Limit = PageSize }, cancellationToken: ct);

                foreach (var position in positions.Data)
                {
                    var isin = position.Instrument.Isin;
                    if (!string.IsNullOrEmpty(isin))
                        quantities[isin] = ParseDecimal(position.Quantity);
                }

                if (offset + positions.Meta.Count >= positions.Meta.TotalCount || positions.Meta.Count == 0)
                    break;
            }

            // ...and current per-instrument value plus the total from the valuation.
            var valuation = await _client.Valuations.GetAccountValuation(
                new GetAccountValuationRequest { AccountId = accountId, PriceQuality = PriceQuality3.HighestAvailable },
                cancellationToken: ct);

            var holdings = new List<HoldingSnapshot>();
            foreach (var sp in valuation.SecurityPositions)
            {
                var isin = sp.Instrument.Isin;
                if (string.IsNullOrEmpty(isin)) continue;

                var quantity = quantities.TryGetValue(isin, out var q) ? q : ParseDecimal(sp.Quantity);
                var value = sp.Value is null ? 0m : ParseDecimal(sp.Value.Amount);
                holdings.Add(new HoldingSnapshot(isin, quantity, value));
            }

            var totalValue = ParseDecimal(valuation.TotalSecurityValue.Amount);
            return new PortfolioSnapshot(holdings, totalValue);
        });

    // ----------------------------------------------------------------- helpers

    private static IReadOnlyList<Allocation> ToAllocations(IReadOnlyList<ModelFund> funds) =>
        funds.Select(f => new Allocation
        {
            InstrumentId = InstrumentId.String(f.InstrumentId),
            InstrumentIdType = InstrumentIdType4.Isin,
            Weight = FormatWeight(f.Weight)
        }).ToList();

    private static ProviderOutcome MapOrderStatus(Status65 status)
    {
        if (status == Status65.Filled || status == Status65.Settled) return ProviderOutcome.Succeeded;
        if (status == Status65.Cancelled) return ProviderOutcome.Failed;
        return ProviderOutcome.Pending; // New, Processing, or any undeclared value
    }

    private static ProviderOutcome DeriveRealignmentOutcome(IReadOnlyCollection<Status71> statuses)
    {
        if (statuses.Count == 0) return ProviderOutcome.Pending;
        if (statuses.Any(s => s == Status71.Cancelled)) return ProviderOutcome.Failed;
        if (statuses.All(s => s == Status71.Filled)) return ProviderOutcome.Succeeded;
        return ProviderOutcome.Pending;
    }

    private static (IReadOnlyList<Status71> Items, Meta? Meta) ReadRebalancingOrders(PortfoliosRebalancingExecutionOrderListResponse response)
    {
        var statuses = response.Data.Select(o => o.Status).ToList();
        return (statuses, response.Meta);
    }

    private static DateTimeOffset ParseDate(string value, string field)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return parsed;
        throw new PortfolioValidationException($"{field} must be an ISO-8601 date.");
    }

    private static decimal ParseDecimal(string value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;

    private static string FormatAmount(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatWeight(decimal weight) => weight.ToString("0.#####", CultureInfo.InvariantCulture);

    private static T Resolve<T>(string isoCode, string field) where T : UpvestInvestmentApi.Core.Enum.OpenStringEnum<T>
    {
        if (TypedEnumResolve<T>(isoCode, out var value)) return value!;
        throw new PortfolioValidationException($"{field} is not a recognised code ('{isoCode}').");
    }

    private static T? ResolveOptional<T>(string? isoCode) where T : UpvestInvestmentApi.Core.Enum.OpenStringEnum<T>
    {
        if (string.IsNullOrWhiteSpace(isoCode)) return null;
        return TypedEnumResolve<T>(isoCode, out var value) ? value : null;
    }

    private static bool TypedEnumResolve<T>(string code, out T? value) where T : UpvestInvestmentApi.Core.Enum.OpenStringEnum<T>
    {
        // ISO codes are declared on the SDK enums in upper case; normalise before matching.
        return UpvestInvestmentApi.Core.Enum.TypedEnum<string, T>.TryGetKnownValue(code.Trim().ToUpperInvariant(), out value);
    }

    private static DocumentType3 ResolveDocumentType(string type)
    {
        var normalized = type.Trim().ToUpperInvariant().Replace(' ', '_').Replace('-', '_');
        return UpvestInvestmentApi.Core.Enum.TypedEnum<string, DocumentType3>.TryGetKnownValue(normalized, out var dt)
            ? dt!
            : DocumentType3.IdCard;
    }

    // ----------------------------------------------------------------- error translation

    private async Task<T> CallAsync<T>(string operation, bool isWrite, CancellationToken ct, Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (ResponseDeserializationException ex)
        {
            // A 2xx we could not read leaves a write's outcome unknown; a non-2xx only lost the detail.
            bool success = (int)ex.StatusCode is >= 200 and < 300;
            throw new UpvestGatewayException(
                $"Upvest returned a response for '{operation}' that could not be processed.",
                (int)ex.StatusCode, outcomeUnknown: success && isWrite, ex);
        }
        catch (ApiException ex)
        {
            throw new UpvestGatewayException($"Upvest rejected '{operation}'.", (int)ex.StatusCode, outcomeUnknown: false, ex);
        }
        catch (SdkTimeoutException ex)
        {
            throw new UpvestGatewayException($"Upvest did not answer '{operation}' in time.", null, outcomeUnknown: isWrite, ex);
        }
        catch (SdkConnectionException ex)
        {
            throw new UpvestGatewayException($"Upvest was unreachable for '{operation}'.", null, outcomeUnknown: isWrite, ex);
        }
        catch (AuthSchemeException ex)
        {
            throw new UpvestGatewayException($"Upvest credentials are missing or were rejected for '{operation}'.", null, outcomeUnknown: false, ex);
        }
    }

    private static UpvestGatewayException Unreadable(string operation) =>
        new($"Upvest returned an unexpected shape for '{operation}'.", null, outcomeUnknown: false);
}
