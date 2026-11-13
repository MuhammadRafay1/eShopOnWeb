using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.ManagedPortfolio;
using Microsoft.Extensions.Logging;
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
using UpvestInvestmentApi.Requests.UserIdentifiers;
using UpvestInvestmentApi.Requests.Users;
using UpvestInvestmentApi.Requests.Valuations;
using UpvestInvestmentApi.Requests.VirtualBankAccounts;

namespace Microsoft.eShopWeb.Infrastructure.ManagedPortfolio;

/// <summary>
/// The only place that talks to Upvest. It builds request bodies from application types, maps responses and
/// statuses back, and translates every SDK exception into <see cref="UpvestIntegrationException"/>. No method
/// here logs personal data; request bodies are never echoed.
/// </summary>
public sealed class ManagedPortfolioService : IManagedPortfolioService
{
    private static readonly TimeSpan CallBudget = TimeSpan.FromSeconds(30);
    private const int MaxPages = 50;

    private readonly UpvestInvestmentApiClient _client;
    private readonly ILogger<ManagedPortfolioService> _logger;

    public ManagedPortfolioService(UpvestInvestmentApiClient client, ILogger<ManagedPortfolioService> logger)
    {
        _client = client;
        _logger = logger;
    }

    // ---------------------------------------------------------------------------------------------
    // Flow 1 — onboarding
    // ---------------------------------------------------------------------------------------------

    public async Task<InvestorOnboardingResult> OnboardInvestorAsync(string shopperKey, InvestorSignup signup, CancellationToken cancellationToken)
    {
        // A stable idempotency key per shopper, so an SDK re-send of the user creation deduplicates.
        var onboardingKey = DeterministicGuid("onboard:" + shopperKey);

        var userBody = BuildUserBody(signup);

        // The user MUST be created; a failure here aborts onboarding (nothing persisted by the caller).
        UserTol createdUser;
        try
        {
            var created = await Bounded(ct => _client.Users.CreateUser(new CreateUserRequest
            {
                IdempotencyKey = onboardingKey,
                Body = UserCreateRequest.UserTolCreateRequest(userBody)
            }, cancellationToken: ct), cancellationToken);

            if (!created.TryGetUserTol(out var user))
            {
                throw new UpvestIntegrationException(
                    "The investment provider returned an unexpected onboarding response.",
                    UpvestFailureKind.ProviderUnavailable);
            }
            createdUser = user;
        }
        catch (ApiException ex)
        {
            throw Translate("Users.CreateUser", ex);
        }
        catch (AuthSchemeException ex)
        {
            throw NotConfigured("Users.CreateUser", ex);
        }
        catch (SdkConnectionException ex)
        {
            // Connection dropped: the user may or may not have been created. Surface as unavailable; a retry
            // reuses the same idempotency key, so it will not create a duplicate.
            _logger.LogWarning("Upvest Users.CreateUser connection failure during onboarding.");
            throw new UpvestIntegrationException(
                "The investment provider is currently unavailable.", UpvestFailureKind.ProviderUnavailable, null, ex);
        }

        var userId = createdUser.Id;
        Guid? accountGroupId = null;
        Guid? accountId = null;
        Status21? accountStatus = null;

        // The remaining steps are best-effort: in this sandbox they require scopes the client is not granted
        // and will return 401. We record what completes and compute status from that; the shopper's status
        // simply stays "pending" until the account is ready.
        await TryStepAsync("UserIdentifiers.CreateIdentifier", async ct =>
        {
            await _client.UserIdentifiers.CreateIdentifier(new CreateIdentifierRequest
            {
                UserId = userId,
                Body = BuildIdentifierBody(signup.IdentityDocument)
            }, cancellationToken: ct);
        }, cancellationToken);

        await TryStepAsync("TaxResidencies.SetTaxResidencies", async ct =>
        {
            await _client.TaxResidencies.SetTaxResidencies(new SetTaxResidenciesRequest
            {
                UserId = userId,
                IdempotencyKey = DeterministicGuid("tax:" + shopperKey),
                Body = new TaxResidenciesSetRequest
                {
                    TaxResidencies = new List<TaxResidencyForCreateRequest>
                    {
                        TaxResidencyForCreateRequest.WithTaxIdentifierNumber(new WithTaxIdentifierNumber
                        {
                            Country = ResolveEnum<Country>("taxCountry", signup.TaxCountry, Country.TryGetKnownValue),
                            TaxIdentifierNumber = signup.TaxId
                        })
                    }
                }
            }, cancellationToken: ct);
        }, cancellationToken);

        await TryStepAsync("AccountGroups.CreateAccountGroup", async ct =>
        {
            var groupResponse = await _client.AccountGroups.CreateAccountGroup(new CreateAccountGroupRequest
            {
                IdempotencyKey = DeterministicGuid("group:" + shopperKey),
                Body = AccountGroupCreateRequest.AccountGroupCreateUserRequest(new AccountGroupCreateUserRequest
                {
                    UserId = userId,
                    Type = Type13.Personal
                })
            }, cancellationToken: ct);

            if (groupResponse.TryGetAccountGroup(out var group))
            {
                accountGroupId = group.Id;
            }
        }, cancellationToken);

        if (accountGroupId.HasValue)
        {
            await TryStepAsync("AccountsApi.CreateAccount", async ct =>
            {
                var accountResponse = await _client.AccountsApi.CreateAccount(new CreateAccountRequest
                {
                    IdempotencyKey = DeterministicGuid("account:" + shopperKey),
                    Body = AccountCreateRequest.AccountCreateUserRequest(new AccountCreateUserRequest
                    {
                        UserId = userId,
                        AccountGroupId = accountGroupId.Value,
                        Type = Type16.Portfolio,
                        Name = "Managed portfolio"
                    })
                }, cancellationToken: ct);

                if (accountResponse.TryGetAccount(out var account))
                {
                    accountId = account.Id;
                    accountStatus = account.Status;
                }
            }, cancellationToken);
        }

        var status = ComputeInvestorStatus(createdUser.Status, accountStatus, accountId.HasValue);
        return new InvestorOnboardingResult(userId, accountGroupId, accountId, status);
    }

    public async Task<InvestorStatus> GetInvestorStatusAsync(Guid userId, Guid? accountId, CancellationToken cancellationToken)
    {
        return await Guarded("Users.RetrieveUser", async ct =>
        {
            var userResponse = await _client.Users.RetrieveUser(new RetrieveUserRequest { UserId = userId }, cancellationToken: ct);
            if (!userResponse.TryGetUserTol(out var user))
            {
                return InvestorStatus.Pending;
            }

            Status21? accountStatus = null;
            if (accountId.HasValue)
            {
                var accountResponse = await _client.AccountsApi.RetrieveAccount(
                    new RetrieveAccountRequest { AccountId = accountId.Value }, cancellationToken: ct);
                if (accountResponse.TryGetAccount(out var account))
                {
                    accountStatus = account.Status;
                }
            }

            return ComputeInvestorStatus(user.Status, accountStatus, accountId.HasValue);
        }, cancellationToken);
    }

    // ---------------------------------------------------------------------------------------------
    // Flow 2 — model portfolios
    // ---------------------------------------------------------------------------------------------

    public Task<Guid> CreateAllocationAsync(string name, IReadOnlyList<ModelFundSpec> funds, CancellationToken cancellationToken) =>
        Guarded("Portfolios.CreatePortfoliosAllocation", async ct =>
        {
            var response = await _client.Portfolios.CreatePortfoliosAllocation(new CreatePortfoliosAllocationRequest
            {
                IdempotencyKey = Guid.NewGuid(),
                Body = new PortfoliosAllocationCreateRequest
                {
                    Name = name,
                    Allocation = BuildAllocation(funds)
                }
            }, cancellationToken: ct);
            return response.Id;
        }, cancellationToken);

    public Task UpdateAllocationAsync(Guid allocationId, string name, IReadOnlyList<ModelFundSpec> funds, CancellationToken cancellationToken) =>
        Guarded("Portfolios.UpdatePortfoliosAllocation", async ct =>
        {
            await _client.Portfolios.UpdatePortfoliosAllocation(new UpdatePortfoliosAllocationRequest
            {
                AllocationId = allocationId,
                Body = new PortfoliosAllocationUpdateRequest
                {
                    Name = name,
                    Allocation = BuildAllocation(funds)
                }
            }, cancellationToken: ct);
            return true;
        }, cancellationToken);

    public Task SetModelChoiceAsync(Guid accountId, Guid allocationId, bool alreadyConfigured, CancellationToken cancellationToken) =>
        Guarded("Portfolios.SetConfiguration", async ct =>
        {
            if (alreadyConfigured)
            {
                await _client.Portfolios.UpdatePortfoliosConfiguration(new UpdatePortfoliosConfigurationRequest
                {
                    AccountId = accountId,
                    IdempotencyKey = Guid.NewGuid(),
                    Body = new PortfoliosConfigurationUpdateRequest { AllocationId = allocationId }
                }, cancellationToken: ct);
            }
            else
            {
                await _client.Portfolios.CreatePortfoliosConfiguration(new CreatePortfoliosConfigurationRequest
                {
                    IdempotencyKey = Guid.NewGuid(),
                    Body = new PortfoliosConfigurationCreateRequest
                    {
                        AccountId = accountId,
                        AllocationId = allocationId
                    }
                }, cancellationToken: ct);
            }
            return true;
        }, cancellationToken);

    // ---------------------------------------------------------------------------------------------
    // Flow 3 — paying in
    // ---------------------------------------------------------------------------------------------

    public async Task<PayInResult> PayInAsync(Guid userId, Guid accountGroupId, Guid accountId, decimal amount, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var amountText = FormatAmount(amount);
        Guid? bankTransactionId = null;

        // Step 1 — credit cash into the sandbox account via the account's virtual bank account IBAN.
        try
        {
            var destinationIban = await Bounded(ct => ResolveAccountIbanAsync(accountGroupId, ct), cancellationToken);
            if (destinationIban is not null)
            {
                var funding = await Bounded(ct => _client.Tests.CreateBankTransaction(new CreateBankTransactionRequest
                {
                    IdempotencyKey = idempotencyKey,
                    Body = new PaymentsBankTransactionCreateRequest
                    {
                        Account = new Account29 { Iban = destinationIban },
                        Amount = amountText,
                        Currency = BankTransactionCurrency.Eur,
                        ClientReference = idempotencyKey.ToString("N"),
                        CounterpartyName = "eShopOnWeb Funding",
                        CounterpartyAccount = new CounterpartyAccount { Iban = "DE89370400440532013000" }
                    }
                }, cancellationToken: ct), cancellationToken);
                bankTransactionId = funding.Id;
            }
        }
        catch (ApiException ex)
        {
            // Funding could not be placed (e.g. missing scope). Log and continue to the invest step, which
            // will surface the outcome.
            _logger.LogWarning("Upvest sandbox funding failed with status {Status}.", (int)ex.StatusCode);
        }
        catch (AuthSchemeException ex)
        {
            throw NotConfigured("Tests.CreateBankTransaction", ex);
        }
        catch (SdkConnectionException)
        {
            _logger.LogWarning("Upvest sandbox funding connection failure; outcome unknown.");
            // The credit may have landed; the invest step is not safe to run on an unknown balance.
            return new PayInResult(null, null, PaymentStatus.Pending);
        }

        // Step 2 — invest the cash across the model's weights with a BUY order.
        try
        {
            var order = await Bounded(ct => _client.Portfolios.CreatePortfoliosOrder(new CreatePortfoliosOrderRequest
            {
                IdempotencyKey = idempotencyKey,
                Body = new PortfoliosOrderPlaceRequest
                {
                    UserId = userId,
                    AccountId = accountId,
                    CashAmount = amountText,
                    Currency = Currency.Eur,
                    Side = Side15.Buy
                }
            }, cancellationToken: ct), cancellationToken);

            return new PayInResult(bankTransactionId, order.Id, MapOrderStatus(order.Status));
        }
        catch (ApiException ex)
        {
            // The provider answered with an error: the order was not created, so the pay-in did not invest.
            _logger.LogWarning("Upvest Portfolios.CreatePortfoliosOrder failed with status {Status}.", (int)ex.StatusCode);
            return new PayInResult(bankTransactionId, null, PaymentStatus.Failed);
        }
        catch (AuthSchemeException ex)
        {
            throw NotConfigured("Portfolios.CreatePortfoliosOrder", ex);
        }
        catch (SdkConnectionException)
        {
            // Connection dropped after the order may have reached Upvest: outcome unknown, leave pending so a
            // later re-read settles it (the idempotency key prevents a duplicate on retry).
            _logger.LogWarning("Upvest Portfolios.CreatePortfoliosOrder connection failure; outcome unknown.");
            return new PayInResult(bankTransactionId, null, PaymentStatus.Pending);
        }
    }

    public Task<PaymentStatus> GetOrderStatusAsync(Guid orderId, CancellationToken cancellationToken) =>
        Guarded("Portfolios.RetrievePortfoliosOrder", async ct =>
        {
            var order = await _client.Portfolios.RetrievePortfoliosOrder(
                new RetrievePortfoliosOrderRequest { PortfolioOrderId = orderId }, cancellationToken: ct);
            return MapOrderStatus(order.Status);
        }, cancellationToken);

    // ---------------------------------------------------------------------------------------------
    // Flow 4 — realignment
    // ---------------------------------------------------------------------------------------------

    public Task<Guid> TriggerRealignmentAsync(Guid allocationId, Guid idempotencyKey, CancellationToken cancellationToken) =>
        Guarded("PortfoliosRebalancing.TriggerPortfolioRebalancing", async ct =>
        {
            var response = await _client.PortfoliosRebalancing.TriggerPortfolioRebalancing(new TriggerPortfolioRebalancingOperationRequest
            {
                IdempotencyKey = idempotencyKey,
                Body = TriggerPortfolioRebalancingRequest.Allocations(new Allocations
                {
                    AllocationsValue = new List<Guid> { allocationId }
                })
            }, cancellationToken: ct);
            return response.Id;
        }, cancellationToken);

    public Task<RealignmentStatus> GetRealignmentStatusAsync(Guid executionId, CancellationToken cancellationToken) =>
        Guarded("PortfoliosRebalancing.ListExecutionOrders", async ct =>
        {
            // The execution object carries no status; derive it by aggregating the statuses of its orders.
            var statuses = new List<Status71>();
            int offset = 0;
            const int limit = 100;
            for (var page = 0; page < MaxPages; page++)
            {
                var response = await _client.PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders(
                    new ListPortfolioRebalancingExecutionOrdersRequest
                    {
                        ExecutionId = executionId,
                        Offset = offset,
                        Limit = limit
                    }, cancellationToken: ct);

                statuses.AddRange(response.Data.Select(o => o.Status));
                offset += response.Data.Count;
                if (response.Data.Count == 0 || offset >= response.Meta.TotalCount)
                {
                    break;
                }
            }

            return DeriveRealignmentStatus(statuses);
        }, cancellationToken);

    // ---------------------------------------------------------------------------------------------
    // Flow 5 — portfolio view
    // ---------------------------------------------------------------------------------------------

    public Task<PortfolioSnapshot> GetPortfolioAsync(Guid accountId, CancellationToken cancellationToken) =>
        Guarded("Valuations.GetAccountValuation", async ct =>
        {
            var valuation = await _client.Valuations.GetAccountValuation(new GetAccountValuationRequest
            {
                AccountId = accountId,
                PriceQuality = PriceQuality3.HighestAvailable
            }, cancellationToken: ct);

            var holdings = valuation.SecurityPositions.Select(p => new PortfolioHolding(
                InstrumentId: p.Instrument.Isin ?? p.Instrument.Uuid.ToString(),
                Quantity: ParseDecimal(p.Quantity),
                Value: p.Value is null ? (decimal?)null : ParseDecimal(p.Value.Amount))).ToList();

            return new PortfolioSnapshot(holdings, ParseDecimal(valuation.TotalSecurityValue.Amount));
        }, cancellationToken);

    // ---------------------------------------------------------------------------------------------
    // Request-body builders
    // ---------------------------------------------------------------------------------------------

    private UserTolCreateRequest BuildUserBody(InvestorSignup s) => new()
    {
        FirstName = s.FirstName,
        LastName = s.LastName,
        Email = s.Email,
        BirthDate = s.BirthDate,
        BirthCity = s.BirthCity,
        BirthCountry = string.IsNullOrWhiteSpace(s.BirthCountry)
            ? null
            : ResolveEnum<BirthCountry>("birthCountry", s.BirthCountry!, BirthCountry.TryGetKnownValue),
        Nationalities = new List<Nationality> { ResolveEnum<Nationality>("nationality", s.Nationality, Nationality.TryGetKnownValue) },
        PhoneNumber = s.PhoneNumber,
        Address = new Address2
        {
            AddressLine1 = s.Address.Line1,
            Postcode = s.Address.Postcode,
            City = s.Address.City,
            Country = ResolveEnum<Country>("address.country", s.Address.Country, Country.TryGetKnownValue)
        },
        // FATCA is required by the SDK but not part of the shop's form: supply a default non-US declaration.
        Fatca = new Fatca { Status = false, ConfirmedAt = DateTimeOffset.UtcNow }
    };

    private IdentifierCreateRequest BuildIdentifierBody(InvestorIdentityDocument doc)
    {
        // Upvest's identifier model has no expiry field (plan Blocker B3); expiryDate is validated at the API
        // boundary and intentionally not forwarded here.
        var type = Type7.NationalId;
        var normalised = doc.Type?.Trim().ToUpperInvariant().Replace(' ', '_') ?? string.Empty;
        if (Type7.TryGetKnownValue(normalised, out var known))
        {
            type = known;
        }

        return new IdentifierCreateRequest
        {
            Type = type,
            IssuingCountry = ResolveEnum<IssuingCountry>("identityDocument.issuingCountry", doc.IssuingCountry, IssuingCountry.TryGetKnownValue),
            Identifier = doc.Number
        };
    }

    private IReadOnlyList<Allocation> BuildAllocation(IReadOnlyList<ModelFundSpec> funds) =>
        funds.Select(f => new Allocation
        {
            InstrumentId = InstrumentId.String(f.InstrumentId),
            InstrumentIdType = InstrumentIdType4.Isin,
            Weight = FormatWeight(f.Weight)
        }).ToList();

    private async Task<string?> ResolveAccountIbanAsync(Guid accountGroupId, CancellationToken ct)
    {
        var response = await _client.VirtualBankAccounts.ListVirtualBankAccounts(
            new ListVirtualBankAccountsRequest { AccountGroupId = accountGroupId }, cancellationToken: ct);

        foreach (var vba in response.Data)
        {
            if (vba.Identification.Swift is { } swift)
            {
                return swift.Iban;
            }
        }
        return null;
    }

    // ---------------------------------------------------------------------------------------------
    // Status mapping
    // ---------------------------------------------------------------------------------------------

    private static InvestorStatus ComputeInvestorStatus(Status userStatus, Status21? accountStatus, bool hasAccount)
    {
        if (userStatus == Status.Offboarded || userStatus == Status.Offboarding)
        {
            return InvestorStatus.Rejected;
        }
        if (hasAccount && accountStatus is not null && accountStatus == Status21.Active)
        {
            return InvestorStatus.Active;
        }
        return InvestorStatus.Pending;
    }

    private static PaymentStatus MapOrderStatus(Status65 status)
    {
        if (status == Status65.Filled || status == Status65.Settled) return PaymentStatus.Invested;
        if (status == Status65.Cancelled) return PaymentStatus.Failed;
        return PaymentStatus.Pending;
    }

    private static RealignmentStatus DeriveRealignmentStatus(IReadOnlyList<Status71> statuses)
    {
        if (statuses.Count == 0) return RealignmentStatus.Pending;

        var anyInFlight = statuses.Any(s => s == Status71.New || s == Status71.Processing);
        if (anyInFlight) return RealignmentStatus.Pending;

        if (statuses.All(s => s == Status71.Filled)) return RealignmentStatus.Completed;

        // No order is still in flight and not all filled: at least one was cancelled.
        return RealignmentStatus.Failed;
    }

    // ---------------------------------------------------------------------------------------------
    // Call plumbing
    // ---------------------------------------------------------------------------------------------

    private async Task<T> Bounded<T>(Func<CancellationToken, Task<T>> call, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(CallBudget);
        return await call(cts.Token);
    }

    private async Task<T> Guarded<T>(string operation, Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        try
        {
            return await Bounded(work, ct);
        }
        catch (ApiException ex)
        {
            throw Translate(operation, ex);
        }
        catch (AuthSchemeException ex)
        {
            throw NotConfigured(operation, ex);
        }
        catch (SdkConnectionException ex)
        {
            _logger.LogWarning("Upvest {Operation} connection failure.", operation);
            throw new UpvestIntegrationException(
                "The investment provider is currently unavailable.", UpvestFailureKind.ProviderUnavailable, null, ex);
        }
    }

    private async Task TryStepAsync(string operation, Func<CancellationToken, Task> step, CancellationToken ct)
    {
        try
        {
            await Bounded<bool>(async innerCt => { await step(innerCt); return true; }, ct);
        }
        catch (ApiException ex)
        {
            // A best-effort onboarding step failed (e.g. a scope the client lacks). The investor simply is
            // not yet ready; the shopper's status stays pending. No personal data is logged.
            _logger.LogWarning("Upvest onboarding step {Operation} did not complete (status {Status}).", operation, (int)ex.StatusCode);
        }
        catch (SdkConnectionException)
        {
            _logger.LogWarning("Upvest onboarding step {Operation} connection failure.", operation);
        }
    }

    private UpvestIntegrationException Translate(string operation, ApiException ex)
    {
        var status = (int)ex.StatusCode;
        _logger.LogWarning("Upvest {Operation} failed with status {Status}.", operation, status);

        // 4xx the caller can act on (but NOT 401/403/429 — those are our credentials/quota, a 5xx to callers).
        if (status is >= 400 and < 500 && status is not 401 and not 403 and not 429)
        {
            return new UpvestIntegrationException(
                "The investment provider rejected the request.", UpvestFailureKind.InvalidRequest, status, ex);
        }

        return new UpvestIntegrationException(
            "The investment provider is currently unavailable.", UpvestFailureKind.ProviderUnavailable, status, ex);
    }

    private UpvestIntegrationException NotConfigured(string operation, AuthSchemeException ex)
    {
        _logger.LogError(ex, "Upvest {Operation} could not authenticate (credentials not configured).", operation);
        return new UpvestIntegrationException(
            "The investment provider is not available.", UpvestFailureKind.ProviderUnavailable, null, ex);
    }

    // ---------------------------------------------------------------------------------------------
    // Value helpers
    // ---------------------------------------------------------------------------------------------

    private delegate bool TryGetKnownValue<T>(string value, out T known);

    private static T ResolveEnum<T>(string field, string raw, TryGetKnownValue<T> resolver)
    {
        var candidate = raw?.Trim().ToUpperInvariant() ?? string.Empty;
        if (resolver(candidate, out var known))
        {
            return known;
        }
        throw new UpvestIntegrationException(
            $"'{field}' is not a value the investment provider accepts.", UpvestFailureKind.InvalidRequest, 400);
    }

    private static string FormatAmount(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatWeight(decimal weight) =>
        weight == 100m ? "100" : weight.ToString("0.#####", CultureInfo.InvariantCulture);

    private static decimal ParseDecimal(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    private static Guid DeterministicGuid(string seed)
    {
        using var md5 = MD5.Create();
        return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(seed)));
    }
}
