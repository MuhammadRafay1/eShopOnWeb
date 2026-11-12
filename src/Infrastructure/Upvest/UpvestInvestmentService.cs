using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
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
using UpvestInvestmentApi.Requests.TaxResidencies;
using UpvestInvestmentApi.Requests.Tests;
using UpvestInvestmentApi.Requests.UserIdentifiers;
using UpvestInvestmentApi.Requests.Users;
using UpvestInvestmentApi.Requests.Valuations;
using UpvestInvestmentApi.Requests.VirtualBankAccounts;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The application's gateway to Upvest. Translates the app's SDK-free contracts to the Upvest SDK, maps
/// SDK failures to <see cref="UpvestIntegrationException"/>, and reads statuses back by re-reading Upvest
/// (no webhook is exposed). Never logs request bodies or personal data.
/// </summary>
public sealed class UpvestInvestmentService : IUpvestInvestmentService
{
    // Sandbox pooling IBAN that receives simulated money-in (from the Upvest payments workflow reference).
    private const string SandboxPoolingIban = "FR7617789000011051089906447";
    // A made-up external payer IBAN for sandbox money-in simulation (not a real account; test data only).
    private const string SandboxCounterpartyIban = "DE89370400440532013000";

    private readonly UpvestInvestmentApiClient _client;
    private readonly IAppLogger<UpvestInvestmentService> _logger;

    public UpvestInvestmentService(UpvestInvestmentApiClient client, IAppLogger<UpvestInvestmentService> logger)
    {
        _client = client;
        _logger = logger;
    }

    // ---------------------------------------------------------------- Flow 1: becoming an investor

    public async Task<InvestorProvisionResult> ProvisionInvestorAsync(InvestorSignup signup, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        // 1) Create the user (users:admin). Required: without a user there is no investor. Personal details — never logged.
        var tol = new UserTolCreateRequest
        {
            FirstName = signup.FirstName,
            LastName = signup.LastName,
            Email = signup.Email,
            BirthDate = ToDateTimeOffset(signup.BirthDate),
            BirthCity = signup.BirthCity,
            BirthCountry = string.IsNullOrWhiteSpace(signup.BirthCountry)
                ? null
                : RequireKnown<UpvestInvestmentApi.Models.Enums.BirthCountry>(signup.BirthCountry, "birthCountry", UpvestInvestmentApi.Models.Enums.BirthCountry.TryGetKnownValue),
            Nationalities = new[] { RequireKnown<Nationality>(signup.Nationality, "nationality", Nationality.TryGetKnownValue) },
            PhoneNumber = string.IsNullOrWhiteSpace(signup.PhoneNumber) ? null : signup.PhoneNumber,
            Address = new Address2
            {
                AddressLine1 = signup.Address.Line1,
                Postcode = signup.Address.Postcode,
                City = signup.Address.City,
                Country = RequireKnown<Country>(signup.Address.Country, "address.country", Country.TryGetKnownValue)
            },
            // The shopper accepted the shop's terms when signing up; record FATCA status (not a US taxpayer) as confirmed now.
            Fatca = new Fatca { Status = false, ConfirmedAt = DateTimeOffset.UtcNow }
        };

        var created = await CallAsync(
            ct => _client.Users.CreateUser(new CreateUserRequest { IdempotencyKey = idempotencyKey, Body = UserCreateRequest.UserTolCreateRequest(tol) }, cancellationToken: ct),
            "CreateUser", isWrite: true, cancellationToken);

        var userId = created.Match(
            onUserByol: b => b.Id,
            onUserTol: t => t.Id,
            otherwise: _ => throw new UpvestIntegrationException("Upvest returned an unrecognised user shape.", UpvestFailureKind.Unknown));

        var userStatus = created.Match(
            onUserByol: b => b.Status.Value,
            onUserTol: t => t.Status.Value,
            otherwise: _ => "UNKNOWN");

        _logger.LogInformation("Upvest CreateUser succeeded: userId {UserId}, status {Status}.", userId, userStatus);

        // 2) Record the identity document the shop verified (users:admin). Best-effort for scope; input errors surface.
        await TrySupportingStepAsync("CreateIdentifier", async () =>
        {
            var body = new IdentifierCreateRequest
            {
                Type = MapDocumentType(signup.IdentityDocument.Type),
                IssuingCountry = RequireKnown<IssuingCountry>(signup.IdentityDocument.IssuingCountry, "identityDocument.issuingCountry", IssuingCountry.TryGetKnownValue),
                Identifier = signup.IdentityDocument.Number
            };
            await CallAsync(ct => _client.UserIdentifiers.CreateIdentifier(
                new CreateIdentifierRequest { UserId = userId, Body = body }, cancellationToken: ct),
                "CreateIdentifier", isWrite: true, cancellationToken);
        });

        // 3) Record the tax residency (taxes:admin — refused by the sandbox client; skipped there).
        await TrySupportingStepAsync("SetTaxResidencies", async () =>
        {
            var body = new TaxResidenciesSetRequest
            {
                TaxResidencies = new[]
                {
                    (TaxResidencyForCreateRequest)new WithTaxIdentifierNumber
                    {
                        Country = RequireKnown<Country>(signup.TaxCountry, "taxCountry", Country.TryGetKnownValue),
                        TaxIdentifierNumber = signup.TaxId
                    }
                }
            };
            await CallAsync(ct => _client.TaxResidencies.SetTaxResidencies(
                new SetTaxResidenciesRequest { UserId = userId, IdempotencyKey = idempotencyKey, Body = body }, cancellationToken: ct),
                "SetTaxResidencies", isWrite: true, cancellationToken);
        });

        // 4) Create a PERSONAL account group and a PORTFOLIO account (accounts:admin — refused by the sandbox
        //    client; skipped there, leaving the investor pending until an account is ready).
        Guid? accountGroupId = null;
        Guid? accountId = null;
        try
        {
            var group = await CallAsync(ct => _client.AccountGroups.CreateAccountGroup(
                new CreateAccountGroupRequest
                {
                    IdempotencyKey = idempotencyKey,
                    Body = AccountGroupCreateRequest.AccountGroupCreateUserRequest(
                        new AccountGroupCreateUserRequest { UserId = userId, Type = Type13.Personal })
                }, cancellationToken: ct),
                "CreateAccountGroup", isWrite: true, cancellationToken);

            accountGroupId = group.Match(
                onAccountGroup: g => g.Id,
                onBusinessAccountGroup: g => g.Id,
                otherwise: _ => throw new UpvestIntegrationException("Upvest returned an unrecognised account-group shape.", UpvestFailureKind.Unknown));

            var account = await CallAsync(ct => _client.AccountsApi.CreateAccount(
                new CreateAccountRequest
                {
                    IdempotencyKey = idempotencyKey,
                    Body = AccountCreateRequest.AccountCreateUserRequest(
                        new AccountCreateUserRequest { UserId = userId, AccountGroupId = accountGroupId.Value, Type = Type16.Portfolio })
                }, cancellationToken: ct),
                "CreateAccount", isWrite: true, cancellationToken);

            accountId = account.Match(
                onAccount: a => a.Id,
                onBusinessAccount: a => a.Id,
                otherwise: _ => throw new UpvestIntegrationException("Upvest returned an unrecognised account shape.", UpvestFailureKind.Unknown));

            _logger.LogInformation("Upvest account provisioned: accountGroupId {GroupId}, accountId {AccountId}.", accountGroupId, accountId);
        }
        catch (UpvestIntegrationException ex) when (ex.Kind == UpvestFailureKind.ProviderRejectedUs)
        {
            _logger.LogWarning("Upvest account provisioning skipped (status {Status}): client not authorised for account scopes. Investor remains pending.", ex.StatusCode ?? 0);
        }

        var status = MapInvestorLifecycle(userStatus, accountReady: accountId is not null);
        return new InvestorProvisionResult(userId, accountGroupId, accountId, status);
    }

    public async Task<InvestorLifecycleStatus> GetInvestorStatusAsync(Guid upvestUserId, bool accountReady, CancellationToken cancellationToken)
    {
        var resp = await CallAsync(ct => _client.Users.RetrieveUser(
            new RetrieveUserRequest { UserId = upvestUserId }, cancellationToken: ct),
            "RetrieveUser", isWrite: false, cancellationToken);

        var statusValue = resp.Match(
            onUserByol: b => b.Status.Value,
            onUserTol: t => t.Status.Value,
            otherwise: _ => "UNKNOWN");

        return MapInvestorLifecycle(statusValue, accountReady);
    }

    // ---------------------------------------------------------------- Flow 2: model portfolios

    public async Task<Guid> UpsertModelAllocationAsync(string name, IReadOnlyList<ModelFundInput> funds, Guid? existingAllocationId, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var allocation = funds.Select(f => new Allocation
        {
            InstrumentId = InstrumentId.String(f.InstrumentId),
            InstrumentIdType = InstrumentIdType4.Isin,
            Weight = FormatWeight(f.Weight)
        }).ToList();

        if (existingAllocationId is { } id)
        {
            var updated = await CallAsync(ct => _client.Portfolios.UpdatePortfoliosAllocation(
                new UpdatePortfoliosAllocationRequest
                {
                    AllocationId = id,
                    Body = new PortfoliosAllocationUpdateRequest { Name = name, Allocation = allocation }
                }, cancellationToken: ct),
                "UpdatePortfoliosAllocation", isWrite: true, cancellationToken);
            return updated.Id;
        }

        var created = await CallAsync(ct => _client.Portfolios.CreatePortfoliosAllocation(
            new CreatePortfoliosAllocationRequest
            {
                IdempotencyKey = idempotencyKey,
                Body = new PortfoliosAllocationCreateRequest { Name = name, Allocation = allocation }
            }, cancellationToken: ct),
            "CreatePortfoliosAllocation", isWrite: true, cancellationToken);
        return created.Id;
    }

    public async Task<IReadOnlyList<string>> FindUnknownInstrumentsAsync(IReadOnlyList<string> isins, CancellationToken cancellationToken)
    {
        var requested = new HashSet<string>(isins, StringComparer.OrdinalIgnoreCase);
        if (requested.Count == 0)
        {
            return Array.Empty<string>();
        }

        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int offset = 0;
        const int pageSize = 100;
        const int maxPages = 100; // bound the walk regardless of provider behaviour
        for (int page = 0; page < maxPages; page++)
        {
            var response = await CallAsync(ct => _client.Instruments.ListInstruments(
                new ListInstrumentsRequest { Offset = offset, Limit = pageSize }, cancellationToken: ct),
                "ListInstruments", isWrite: false, cancellationToken);

            foreach (var instrument in response.Data)
            {
                if (!string.IsNullOrEmpty(instrument.Isin))
                {
                    known.Add(instrument.Isin);
                }
            }

            offset += response.Data.Count;
            if (response.Data.Count == 0 || offset >= response.Meta.TotalCount)
            {
                break;
            }
        }

        return requested.Where(isin => !known.Contains(isin)).ToList();
    }

    public async Task FollowModelAsync(Guid accountId, Guid allocationId, bool configurationExists, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        if (configurationExists)
        {
            await CallAsync(ct => _client.Portfolios.UpdatePortfoliosConfiguration(
                new UpdatePortfoliosConfigurationRequest
                {
                    AccountId = accountId,
                    IdempotencyKey = idempotencyKey,
                    Body = new PortfoliosConfigurationUpdateRequest { AllocationId = allocationId }
                }, cancellationToken: ct),
                "UpdatePortfoliosConfiguration", isWrite: true, cancellationToken);
            return;
        }

        await CallAsync(ct => _client.Portfolios.CreatePortfoliosConfiguration(
            new CreatePortfoliosConfigurationRequest
            {
                IdempotencyKey = idempotencyKey,
                Body = new PortfoliosConfigurationCreateRequest { AccountId = accountId, AllocationId = allocationId }
            }, cancellationToken: ct),
            "CreatePortfoliosConfiguration", isWrite: true, cancellationToken);
    }

    // ---------------------------------------------------------------- Flow 3: paying in

    public async Task<PaymentPlacementResult> PayInAsync(InvestorPayIn payIn, CancellationToken cancellationToken)
    {
        // Ensure a virtual bank account exists for this account group (created once, then reused).
        var virtualBankAccountId = payIn.VirtualBankAccountId;
        if (virtualBankAccountId is null)
        {
            var vban = await CallAsync(ct => _client.VirtualBankAccounts.CreateVirtualBankAccount(
                new CreateVirtualBankAccountRequest
                {
                    IdempotencyKey = payIn.VirtualBankAccountIdempotencyKey,
                    Body = new PaymentsVirtualBankAccountCreateRequest
                    {
                        AccountGroupId = payIn.AccountGroupId,
                        Name = "eShop managed portfolio"
                    }
                }, cancellationToken: ct),
                "CreateVirtualBankAccount", isWrite: true, cancellationToken);
            virtualBankAccountId = vban.Id;
        }

        // Simulate money reaching the account group in the sandbox (no real money moves).
        var amount = FormatMoney(payIn.Amount);
        var clientReference = payIn.OrderIdempotencyKey.ToString("N");
        await CallAsync(ct => _client.Tests.CreateBankTransaction(
            new CreateBankTransactionRequest
            {
                IdempotencyKey = payIn.MoneyInIdempotencyKey,
                Body = new PaymentsBankTransactionCreateRequest
                {
                    Account = new Account29 { Iban = SandboxPoolingIban },
                    Amount = amount,
                    Currency = BankTransactionCurrency.Eur,
                    ClientReference = clientReference,
                    CounterpartyName = payIn.OwnerName,
                    CounterpartyAccount = new CounterpartyAccount { Iban = SandboxCounterpartyIban },
                    VirtualBankAccountId = virtualBankAccountId
                }
            }, cancellationToken: ct),
            "CreateBankTransaction", isWrite: true, cancellationToken);

        // Invest the cash across the followed model's weights (BUY, portfolio-level order).
        var order = await CallAsync(ct => _client.Portfolios.CreatePortfoliosOrder(
            new CreatePortfoliosOrderRequest
            {
                IdempotencyKey = payIn.OrderIdempotencyKey,
                Body = new PortfoliosOrderPlaceRequest
                {
                    UserId = payIn.UserId,
                    AccountId = payIn.AccountId,
                    CashAmount = amount,
                    Currency = Currency.Eur,
                    Side = Side15.Buy
                }
            }, cancellationToken: ct),
            "CreatePortfoliosOrder", isWrite: true, cancellationToken);

        _logger.LogInformation("Upvest invest order placed: orderId {OrderId}, status {Status}.", order.Id, order.Status.Value);
        return new PaymentPlacementResult(order.Id, MapPaymentStatus(order.Status.Value), virtualBankAccountId.Value);
    }

    public async Task<PaymentStatus> GetOrderStatusAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await CallAsync(ct => _client.Portfolios.RetrievePortfoliosOrder(
            new RetrievePortfoliosOrderRequest { PortfolioOrderId = orderId }, cancellationToken: ct),
            "RetrievePortfoliosOrder", isWrite: false, cancellationToken);
        return MapPaymentStatus(order.Status.Value);
    }

    // ---------------------------------------------------------------- Flow 4: realignment

    public async Task<Guid> TriggerRealignmentAsync(Guid allocationId, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var response = await CallAsync(ct => _client.PortfoliosRebalancing.TriggerPortfolioRebalancing(
            new TriggerPortfolioRebalancingOperationRequest
            {
                IdempotencyKey = idempotencyKey,
                Body = TriggerPortfolioRebalancingRequest.Allocations(
                    new Allocations { AllocationsValue = new[] { allocationId } })
            }, cancellationToken: ct),
            "TriggerPortfolioRebalancing", isWrite: true, cancellationToken);
        return response.Id;
    }

    public async Task<RealignmentStatus> GetRealignmentStatusAsync(Guid executionId, CancellationToken cancellationToken)
    {
        // Confirm the execution exists (it carries no status field of its own) ...
        await CallAsync(ct => _client.PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution(
            new RetrievePortfoliosRebalancingExecutionRequest { ExecutionId = executionId }, cancellationToken: ct),
            "RetrievePortfoliosRebalancingExecution", isWrite: false, cancellationToken);

        // ... then synthesise status from the execution's orders: any cancelled => failed; all filled => completed; else pending.
        bool anyFailed = false;
        bool anyPending = false;
        int total = 0;
        int offset = 0;
        const int pageSize = 100;
        const int maxPages = 100;
        for (int page = 0; page < maxPages; page++)
        {
            var response = await CallAsync(ct => _client.PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders(
                new ListPortfolioRebalancingExecutionOrdersRequest { ExecutionId = executionId, Offset = offset, Limit = pageSize }, cancellationToken: ct),
                "ListPortfolioRebalancingExecutionOrders", isWrite: false, cancellationToken);

            foreach (var o in response.Data)
            {
                total++;
                switch (o.Status.Value)
                {
                    case "CANCELLED": anyFailed = true; break;
                    case "FILLED": break; // terminal success
                    default: anyPending = true; break; // NEW / PROCESSING / anything else
                }
            }

            offset += response.Data.Count;
            if (response.Data.Count == 0 || offset >= response.Meta.TotalCount)
            {
                break;
            }
        }

        if (anyFailed) return RealignmentStatus.Failed;
        if (total > 0 && !anyPending) return RealignmentStatus.Completed;
        return RealignmentStatus.Pending;
    }

    // ---------------------------------------------------------------- Flow 5: the portfolio

    public async Task<RemotePortfolio> GetPortfolioAsync(Guid accountId, CancellationToken cancellationToken)
    {
        Guid? allocationId = null;
        try
        {
            var config = await CallAsync(ct => _client.Portfolios.RetrievePortfoliosConfiguration(
                new RetrievePortfoliosConfigurationRequest { AccountId = accountId }, cancellationToken: ct),
                "RetrievePortfoliosConfiguration", isWrite: false, cancellationToken);
            allocationId = config.AllocationId;
        }
        catch (UpvestIntegrationException ex) when (ex.StatusCode == 404)
        {
            // No configuration yet — the account follows no model.
        }

        var valuation = await CallAsync(ct => _client.Valuations.GetAccountValuation(
            new GetAccountValuationRequest { AccountId = accountId, PriceQuality = PriceQuality3.HighestAvailable }, cancellationToken: ct),
            "GetAccountValuation", isWrite: false, cancellationToken);

        var holdings = valuation.SecurityPositions.Select(p => new RemoteHolding(
            InstrumentId: p.Instrument.Isin ?? p.Instrument.Uuid.ToString(),
            Quantity: ParseDecimal(p.Quantity),
            Value: p.Value is null ? 0m : ParseDecimal(p.Value.Amount))).ToList();

        var totalValue = ParseDecimal(valuation.TotalSecurityValue.Amount);
        return new RemotePortfolio(allocationId, holdings, totalValue);
    }

    // ---------------------------------------------------------------- helpers

    private delegate bool TryGetKnown<TEnum>(string? value, out TEnum? result);

    private static TEnum RequireKnown<TEnum>(string? code, string field, TryGetKnown<TEnum> tryGet) where TEnum : class
    {
        if (!string.IsNullOrWhiteSpace(code) && tryGet(code!.Trim().ToUpperInvariant(), out var value) && value is not null)
        {
            return value;
        }
        throw new UpvestIntegrationException($"'{field}' is not a value Upvest recognises.", UpvestFailureKind.CallerError, 400);
    }

    private static Type7 MapDocumentType(string? type)
    {
        if (!string.IsNullOrWhiteSpace(type) && type.Trim().ToUpperInvariant().Contains("PASSPORT"))
        {
            return Type7.Passport;
        }
        return Type7.NationalId;
    }

    private static DateTimeOffset ToDateTimeOffset(DateOnly date) =>
        new(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero);

    // Money: euros, always two decimals (matches the amount regexes). Weight: trimmed (model weight 100 must be "100").
    private static string FormatMoney(decimal euros) => euros.ToString("0.00", CultureInfo.InvariantCulture);
    private static string FormatWeight(decimal weight) => weight.ToString("0.#####", CultureInfo.InvariantCulture);

    private static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;

    private static InvestorLifecycleStatus MapInvestorLifecycle(string upvestUserStatus, bool accountReady)
    {
        switch (upvestUserStatus)
        {
            case "OFFBOARDING":
            case "OFFBOARDED":
                return InvestorLifecycleStatus.Rejected;
            case "ACTIVE" when accountReady:
                return InvestorLifecycleStatus.Active;
            default:
                return InvestorLifecycleStatus.Pending;
        }
    }

    private static PaymentStatus MapPaymentStatus(string orderStatus) => orderStatus switch
    {
        "FILLED" or "SETTLED" => PaymentStatus.Invested,
        "CANCELLED" => PaymentStatus.Failed,
        _ => PaymentStatus.Pending // NEW / PROCESSING / anything else
    };

    /// <summary>
    /// Run a supplementary onboarding step (tax residency, identity document). These advance the user toward
    /// active but are not the critical path — the shopper becomes an investor on the strength of the created
    /// user. A failure here (a refused scope in the sandbox, or provider-side validation of supplementary
    /// data) is logged and does not fail the sign-up.
    /// </summary>
    private async Task TrySupportingStepAsync(string step, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (UpvestIntegrationException ex)
        {
            _logger.LogWarning("Upvest {Step} not completed (status {Status}); sign-up continues.", step, ex.StatusCode ?? 0);
        }
    }

    /// <summary>Invoke an SDK call, bounding the whole call by the request's cancellation token and translating every SDK failure to <see cref="UpvestIntegrationException"/>.</summary>
    private async Task<T> CallAsync<T>(Func<CancellationToken, Task<T>> operation, string operationName, bool isWrite, CancellationToken cancellationToken)
    {
        try
        {
            return await operation(cancellationToken);
        }
        catch (AuthSchemeException ex)
        {
            _logger.LogWarning("Upvest {Operation} failed: credentials could not be applied.", operationName);
            throw new UpvestIntegrationException("The investment provider could not be authenticated.", UpvestFailureKind.ProviderRejectedUs, innerException: ex);
        }
        catch (ApiException ex) // covers ApiException<TError> and ResponseDeserializationException; status is always present
        {
            var status = (int)ex.StatusCode;
            var kind = ClassifyStatus(status, isWrite);
            _logger.LogWarning("Upvest {Operation} returned HTTP {Status}.", operationName, status);
            throw new UpvestIntegrationException(CallerMessageFor(kind), kind, status, ex);
        }
        catch (SdkTimeoutException ex)
        {
            _logger.LogWarning("Upvest {Operation} timed out after {Timeout}.", operationName, ex.Timeout);
            throw new UpvestIntegrationException(
                isWrite ? "The investment provider did not respond in time; the outcome is unknown." : "The investment provider did not respond in time.",
                isWrite ? UpvestFailureKind.Unknown : UpvestFailureKind.ProviderUnavailable, innerException: ex);
        }
        catch (SdkConnectionException ex)
        {
            _logger.LogWarning("Upvest {Operation} could not reach the provider.", operationName);
            throw new UpvestIntegrationException(
                isWrite ? "The investment provider could not be reached; the outcome is unknown." : "The investment provider could not be reached.",
                isWrite ? UpvestFailureKind.Unknown : UpvestFailureKind.ProviderUnavailable, innerException: ex);
        }
    }

    private static UpvestFailureKind ClassifyStatus(int status, bool isWrite) => status switch
    {
        401 or 403 => UpvestFailureKind.ProviderRejectedUs, // our credentials/scopes — not the caller's fault
        429 => UpvestFailureKind.ProviderRejectedUs,        // our quota
        >= 400 and < 500 => UpvestFailureKind.CallerError,  // the provider rejected the caller's request
        >= 200 and < 300 => UpvestFailureKind.Unknown,      // a 2xx body we could not deserialize
        _ => UpvestFailureKind.ProviderUnavailable          // 5xx and anything else
    };

    private static string CallerMessageFor(UpvestFailureKind kind) => kind switch
    {
        UpvestFailureKind.CallerError => "The investment provider rejected the request.",
        UpvestFailureKind.ProviderRejectedUs => "The investment provider is currently unavailable.",
        UpvestFailureKind.Unknown => "The investment provider returned a response that could not be processed.",
        _ => "The investment provider is currently unavailable."
    };
}
