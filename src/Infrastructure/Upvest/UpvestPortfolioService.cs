using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
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
using UpvestInvestmentApi.Requests.TaxResidencies;
using UpvestInvestmentApi.Requests.TopUps;
using UpvestInvestmentApi.Requests.Users;
using UpvestInvestmentApi.Requests.Valuations;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The Upvest boundary. Builds request records per the SDK contract, reads responses through their union
/// accessors, and translates every SDK/transport failure into <see cref="UpvestIntegrationException"/>
/// (carrying Upvest's HTTP status). No SDK type escapes this class.
/// </summary>
public class UpvestPortfolioService : IUpvestPortfolioService
{
    private readonly UpvestInvestmentApiClient _client;

    public UpvestPortfolioService(UpvestInvestmentApiClient client)
    {
        _client = client;
    }

    // ------------------------------------------------------------------ Flow 1

    public Task<Guid> CreateUserAsync(InvestorOnboardingData data, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var body = new UserTolCreateRequest
        {
            FirstName = data.FirstName,
            LastName = data.LastName,
            Email = data.Email,
            BirthDate = ToDate(data.BirthDate),
            BirthCity = data.BirthCity,
            BirthCountry = data.BirthCountry is null ? null : MapEnum<BirthCountry>(BirthCountry.TryGetKnownValue, data.BirthCountry, "birthCountry"),
            Nationalities = new[] { MapEnum<Nationality>(Nationality.TryGetKnownValue, data.Nationality, "nationality") },
            Address = new Address2
            {
                AddressLine1 = data.AddressLine1,
                Postcode = data.Postcode,
                City = data.City,
                Country = MapEnum<Country>(Country.TryGetKnownValue, data.Country, "address.country"),
            },
            PhoneNumber = string.IsNullOrWhiteSpace(data.PhoneNumber) ? null : data.PhoneNumber,
            // FATCA status is required by Upvest; the shop collects a non-US default and records the confirmation time.
            Fatca = new Fatca { Status = false, ConfirmedAt = DateTimeOffset.UtcNow },
        };

        return CallAsync("CreateUser", isWrite: true, async () =>
        {
            var response = await _client.Users.CreateUser(
                new CreateUserRequest { IdempotencyKey = idempotencyKey, Body = UserCreateRequest.UserTolCreateRequest(body) },
                cancellationToken: cancellationToken);

            // The response is an AnyOf(UserTol, UserByol); either arm may match the wire body. Read the id from whichever.
            if (response.TryGetUserTol(out var tol)) return tol.Id;
            if (response.TryGetUserByol(out var byol)) return byol.Id;
            throw new UpvestIntegrationException("Upvest returned an unexpected user shape on creation.");
        });
    }

    public Task SetTaxResidenciesAsync(Guid userId, string taxCountry, string taxId, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var residency = TaxResidencyForCreateRequest.WithTaxIdentifierNumber(new WithTaxIdentifierNumber
        {
            Country = MapEnum<Country>(Country.TryGetKnownValue, taxCountry, "taxCountry"),
            TaxIdentifierNumber = taxId,
        });

        return CallAsync("SetTaxResidencies", isWrite: true, async () =>
        {
            await _client.TaxResidencies.SetTaxResidencies(
                new SetTaxResidenciesRequest
                {
                    UserId = userId,
                    IdempotencyKey = idempotencyKey,
                    Body = new TaxResidenciesSetRequest { TaxResidencies = new[] { residency } },
                },
                cancellationToken: cancellationToken);
            return true;
        });
    }

    public Task<Guid> CreateAccountGroupAsync(Guid userId, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        return CallAsync("CreateAccountGroup", isWrite: true, async () =>
        {
            var response = await _client.AccountGroups.CreateAccountGroup(
                new CreateAccountGroupRequest
                {
                    IdempotencyKey = idempotencyKey,
                    Body = AccountGroupCreateRequest.AccountGroupCreateUserRequest(new AccountGroupCreateUserRequest
                    {
                        UserId = userId,
                        Type = Type13.Personal,
                    }),
                },
                cancellationToken: cancellationToken);

            if (response.TryGetAccountGroup(out var group)) return group.Id;
            throw new UpvestIntegrationException("Upvest returned an unexpected account-group shape on creation.");
        });
    }

    public Task<Guid> CreateAccountAsync(Guid userId, Guid accountGroupId, string? name, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        return CallAsync("CreateAccount", isWrite: true, async () =>
        {
            var response = await _client.AccountsApi.CreateAccount(
                new CreateAccountRequest
                {
                    IdempotencyKey = idempotencyKey,
                    Body = AccountCreateRequest.AccountCreateUserRequest(new AccountCreateUserRequest
                    {
                        UserId = userId,
                        AccountGroupId = accountGroupId,
                        Type = Type16.Portfolio,
                        Name = name,
                    }),
                },
                cancellationToken: cancellationToken);

            if (response.TryGetAccount(out var account)) return account.Id;
            throw new UpvestIntegrationException("Upvest returned an unexpected account shape on creation.");
        });
    }

    public async Task<InvestorStatus> GetInvestorStatusAsync(Guid userId, Guid? accountId, CancellationToken cancellationToken)
    {
        var userResponse = await CallAsync("RetrieveUser", isWrite: false, () =>
            _client.Users.RetrieveUser(new RetrieveUserRequest { UserId = userId }, cancellationToken: cancellationToken));

        // The response is an AnyOf(UserTol, UserByol); read the status from whichever arm matched.
        Status? userStatus = null;
        if (userResponse.TryGetUserTol(out var tol)) userStatus = tol.Status;
        else if (userResponse.TryGetUserByol(out var byol)) userStatus = byol.Status;

        if (userStatus == Status.Offboarded || userStatus == Status.Offboarding)
        {
            return InvestorStatus.Rejected;
        }

        if (accountId.HasValue)
        {
            try
            {
                var accountResponse = await CallAsync("RetrieveAccount", isWrite: false, () =>
                    _client.AccountsApi.RetrieveAccount(new RetrieveAccountRequest { AccountId = accountId.Value }, cancellationToken: cancellationToken));

                if (accountResponse.TryGetAccount(out var account) && account.Status == Status21.Active)
                {
                    return InvestorStatus.Active;
                }
            }
            catch (UpvestIntegrationException ex) when (ex.IsAuthorizationFailure)
            {
                // The account's readiness cannot be read with the available scopes — treat as not yet ready.
            }
        }

        return InvestorStatus.Pending;
    }

    // ------------------------------------------------------------------ Flow 2

    public async Task<InstrumentCheck> CheckInstrumentsAsync(IReadOnlyCollection<string> isins, CancellationToken cancellationToken)
    {
        const int pageLimit = 1000;
        const int maxPages = 50; // bound: never page Upvest's instrument catalogue unboundedly.

        try
        {
            var requested = new HashSet<string>(isins, StringComparer.OrdinalIgnoreCase);
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var offset = 0;

            for (var page = 0; page < maxPages; page++)
            {
                var response = await CallAsync("ListInstruments", isWrite: false, () =>
                    _client.Instruments.ListInstruments(new ListInstrumentsRequest { Offset = offset, Limit = pageLimit }, cancellationToken: cancellationToken));

                foreach (var instrument in response.Data)
                {
                    if (instrument.Isin is not null && requested.Contains(instrument.Isin))
                    {
                        found.Add(instrument.Isin);
                    }
                }

                offset += response.Data.Count;
                if (response.Data.Count < pageLimit || offset >= response.Meta.TotalCount || found.Count == requested.Count)
                {
                    break;
                }
            }

            var unknown = requested.Where(i => !found.Contains(i)).ToArray();
            return new InstrumentCheck(Available: true, UnknownIsins: unknown);
        }
        catch (UpvestIntegrationException)
        {
            // The instrument list is not available (e.g. missing instruments:read scope) — caller degrades to format-only.
            return new InstrumentCheck(Available: false, UnknownIsins: Array.Empty<string>());
        }
    }

    public Task<Guid> CreateModelAllocationAsync(string name, IReadOnlyList<FundWeight> funds, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        return CallAsync("CreatePortfoliosAllocation", isWrite: true, async () =>
        {
            var response = await _client.Portfolios.CreatePortfoliosAllocation(
                new CreatePortfoliosAllocationRequest
                {
                    IdempotencyKey = idempotencyKey,
                    Body = new PortfoliosAllocationCreateRequest { Name = name, Allocation = ToAllocations(funds) },
                },
                cancellationToken: cancellationToken);
            return response.Id;
        });
    }

    public Task UpdateModelAllocationAsync(Guid allocationId, string name, IReadOnlyList<FundWeight> funds, CancellationToken cancellationToken)
    {
        return CallAsync("UpdatePortfoliosAllocation", isWrite: true, async () =>
        {
            await _client.Portfolios.UpdatePortfoliosAllocation(
                new UpdatePortfoliosAllocationRequest
                {
                    AllocationId = allocationId,
                    Body = new PortfoliosAllocationUpdateRequest { Name = name, Allocation = ToAllocations(funds) },
                },
                cancellationToken: cancellationToken);
            return true;
        });
    }

    public Task CreateInvestorConfigurationAsync(Guid accountId, Guid allocationId, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        return CallAsync("CreatePortfoliosConfiguration", isWrite: true, async () =>
        {
            await _client.Portfolios.CreatePortfoliosConfiguration(
                new CreatePortfoliosConfigurationRequest
                {
                    IdempotencyKey = idempotencyKey,
                    Body = new PortfoliosConfigurationCreateRequest { AccountId = accountId, AllocationId = allocationId },
                },
                cancellationToken: cancellationToken);
            return true;
        });
    }

    public Task UpdateInvestorConfigurationAsync(Guid accountId, Guid allocationId, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        return CallAsync("UpdatePortfoliosConfiguration", isWrite: true, async () =>
        {
            await _client.Portfolios.UpdatePortfoliosConfiguration(
                new UpdatePortfoliosConfigurationRequest
                {
                    AccountId = accountId,
                    IdempotencyKey = idempotencyKey,
                    Body = new PortfoliosConfigurationUpdateRequest { AllocationId = allocationId },
                },
                cancellationToken: cancellationToken);
            return true;
        });
    }

    // ------------------------------------------------------------------ Flow 3

    public Task<Guid> CreateTopUpAsync(Guid accountGroupId, decimal amount, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        return CallAsync("CreateTopup", isWrite: true, async () =>
        {
            var response = await _client.TopUps.CreateTopup(
                new CreateTopupRequest
                {
                    IdempotencyKey = idempotencyKey,
                    Body = new PaymentsTopUpCreateRequest
                    {
                        AccountGroupId = accountGroupId,
                        CashAmount = UpvestValueFormatting.CashAmount(amount),
                        Currency = Currency.Eur,
                    },
                },
                cancellationToken: cancellationToken);
            return response.Id;
        });
    }

    public Task<Guid> PlacePortfolioOrderAsync(Guid userId, Guid accountId, decimal amount, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        return CallAsync("CreatePortfoliosOrder", isWrite: true, async () =>
        {
            var response = await _client.Portfolios.CreatePortfoliosOrder(
                new CreatePortfoliosOrderRequest
                {
                    IdempotencyKey = idempotencyKey,
                    Body = new PortfoliosOrderPlaceRequest
                    {
                        UserId = userId,
                        AccountId = accountId,
                        CashAmount = UpvestValueFormatting.CashAmount(amount),
                        Currency = Currency.Eur,
                        Side = Side15.Buy,
                    },
                },
                cancellationToken: cancellationToken);
            return response.Id;
        });
    }

    public async Task<PaymentStatus> GetPaymentStatusAsync(Guid? topUpId, Guid? orderId, CancellationToken cancellationToken)
    {
        if (orderId.HasValue)
        {
            var order = await CallAsync("RetrievePortfoliosOrder", isWrite: false, () =>
                _client.Portfolios.RetrievePortfoliosOrder(new RetrievePortfoliosOrderRequest { PortfolioOrderId = orderId.Value }, cancellationToken: cancellationToken));

            if (order.Status == Status65.Filled || order.Status == Status65.Settled) return PaymentStatus.Invested;
            if (order.Status == Status65.Cancelled) return PaymentStatus.Failed;
            return PaymentStatus.Pending;
        }

        if (topUpId.HasValue)
        {
            var topUp = await CallAsync("RetrieveTopup", isWrite: false, () =>
                _client.TopUps.RetrieveTopup(new RetrieveTopupRequest { TopupId = topUpId.Value }, cancellationToken: cancellationToken));

            if (topUp.Status == Status29.Cancelled) return PaymentStatus.Failed;
            // Top-up confirmed/settled but not yet invested — still in flight.
            return PaymentStatus.Pending;
        }

        return PaymentStatus.Pending;
    }

    // ------------------------------------------------------------------ Flow 4

    public Task<Guid> TriggerRealignmentAsync(Guid allocationId, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        return CallAsync("TriggerPortfolioRebalancing", isWrite: true, async () =>
        {
            var response = await _client.PortfoliosRebalancing.TriggerPortfolioRebalancing(
                new TriggerPortfolioRebalancingOperationRequest
                {
                    IdempotencyKey = idempotencyKey,
                    Body = TriggerPortfolioRebalancingRequest.Allocations(new Allocations { AllocationsValue = new[] { allocationId } }),
                },
                cancellationToken: cancellationToken);
            return response.Id;
        });
    }

    public async Task<RealignmentStatus> GetRealignmentStatusAsync(Guid executionId, CancellationToken cancellationToken)
    {
        // The execution resource carries no status, so confirm it exists then derive the outcome from its orders.
        await CallAsync("RetrievePortfoliosRebalancingExecution", isWrite: false, () =>
            _client.PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution(
                new RetrievePortfoliosRebalancingExecutionRequest { ExecutionId = executionId }, cancellationToken: cancellationToken));

        var orders = await CallAsync("ListPortfolioRebalancingExecutionOrders", isWrite: false, () =>
            _client.PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders(
                new ListPortfolioRebalancingExecutionOrdersRequest { ExecutionId = executionId, Limit = 1000 }, cancellationToken: cancellationToken));

        if (orders.Data.Count == 0) return RealignmentStatus.Pending;
        if (orders.Data.Any(o => o.Status == Status71.Cancelled)) return RealignmentStatus.Failed;
        if (orders.Data.All(o => o.Status == Status71.Filled)) return RealignmentStatus.Completed;
        return RealignmentStatus.Pending;
    }

    // ------------------------------------------------------------------ Flow 5

    public async Task<PortfolioView> GetPortfolioAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var configuration = await CallAsync("RetrievePortfoliosConfiguration", isWrite: false, () =>
            _client.Portfolios.RetrievePortfoliosConfiguration(new RetrievePortfoliosConfigurationRequest { AccountId = accountId }, cancellationToken: cancellationToken));

        var valuation = await CallAsync("GetAccountValuation", isWrite: false, () =>
            _client.Valuations.GetAccountValuation(new GetAccountValuationRequest { AccountId = accountId, PriceQuality = PriceQuality3.HighestAvailable }, cancellationToken: cancellationToken));

        var holdings = valuation.SecurityPositions
            .Select(p => new PortfolioHolding(
                InstrumentId: p.Instrument.Isin ?? p.Instrument.Uuid.ToString(),
                Quantity: UpvestValueFormatting.ParseDecimal(p.Quantity),
                Value: UpvestValueFormatting.ParseDecimal(p.Value?.Amount)))
            .ToList();

        var totalValue = UpvestValueFormatting.ParseDecimal(valuation.TotalSecurityValue.Amount);
        return new PortfolioView(configuration.AllocationId, holdings, totalValue);
    }

    // ------------------------------------------------------------------ Helpers

    private static DateTimeOffset ToDate(DateOnly date) =>
        new(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero);

    private static IReadOnlyList<Allocation> ToAllocations(IReadOnlyList<FundWeight> funds) =>
        funds.Select(f => new Allocation
        {
            InstrumentId = InstrumentId.String(f.Isin),
            InstrumentIdType = InstrumentIdType4.Isin,
            Weight = UpvestValueFormatting.Weight(f.Weight),
        }).ToList();

    private delegate bool KnownValueLookup<T>(string? value, out T? known) where T : class;

    /// <summary>Map a caller-supplied code to an open enum's known value, or reject it as invalid input (400).</summary>
    private static T MapEnum<T>(KnownValueLookup<T> tryGetKnownValue, string value, string field) where T : class
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (tryGetKnownValue(normalized, out var known) && known is not null)
        {
            return known;
        }
        throw new PortfolioValidationException($"'{value}' is not an accepted value for {field}.");
    }

    /// <summary>
    /// One error boundary for every Upvest call. Translates the SDK exception family into
    /// <see cref="UpvestIntegrationException"/> with Upvest's HTTP status, marking a write whose outcome
    /// cannot be known (transport failure / timeout, or an unreadable 2xx) so callers reconcile rather than report failure.
    /// </summary>
    private static async Task<T> CallAsync<T>(string operation, bool isWrite, Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (ResponseDeserializationException ex)
        {
            var unknown = isWrite && IsSuccess(ex.StatusCode);
            throw new UpvestIntegrationException(
                $"Upvest returned a response for {operation} that could not be processed.", ex.StatusCode, ex, outcomeUnknown: unknown);
        }
        catch (ApiException ex)
        {
            // Covers both ApiException<RawError> and ApiException<{Operation}Error>; StatusCode is always present.
            throw new UpvestIntegrationException($"Upvest rejected {operation}.", ex.StatusCode, ex);
        }
        catch (AuthSchemeException ex)
        {
            // The client's credentials/scopes were refused (e.g. the token request for a scope this client
            // is not authorised for). Marked so callers can degrade gracefully rather than hard-fail.
            throw new UpvestIntegrationException($"Upvest credentials/scopes were refused for {operation}.", null, ex, credentialOrScopeRefused: true);
        }
        catch (SdkTimeoutException ex)
        {
            throw new UpvestIntegrationException($"Upvest did not answer {operation} in time.", null, ex, outcomeUnknown: isWrite);
        }
        catch (SdkConnectionException ex)
        {
            throw new UpvestIntegrationException($"Upvest was unreachable for {operation}.", null, ex, outcomeUnknown: isWrite);
        }
    }

    private static bool IsSuccess(HttpStatusCode status) => (int)status is >= 200 and < 300;
}
