using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
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
using UpvestInvestmentApi.Requests.Tests;
using UpvestInvestmentApi.Requests.TaxResidencies;
using UpvestInvestmentApi.Requests.UserChecks;
using UpvestInvestmentApi.Requests.Users;
using UpvestInvestmentApi.Requests.Valuations;
using UpvestInvestmentApi.Requests.VirtualBankAccounts;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio;

/// <summary>
/// The Upvest implementation of <see cref="IUpvestPortfolioGateway"/>. Every call is bounded by a
/// per-call budget, every failure is translated to <see cref="UpvestGatewayException"/>, and nothing
/// personal is ever written to a log. The Upvest SDK types never leak past this class.
/// </summary>
public sealed class UpvestPortfolioGateway : IUpvestPortfolioGateway
{
    // Fixed Upvest sandbox values for simulated SEPA credit (from the payments workflow package).
    private const string SandboxPoolingIban = "FR7617789000011051089906447";
    private const string SandboxCounterpartyIban = "DE89370400440532013000";

    // KYC evidence/provider defaults. The shop verified the shopper out-of-band; these record how.
    // Not secrets, not personal data. YOUR CALL — not in the map.
    private const string KycProvider = "eShopOnWeb Identity Verification";

    private readonly UpvestInvestmentApiClient _client;
    private readonly ILogger<UpvestPortfolioGateway> _logger;
    private readonly TimeSpan _budget;

    public UpvestPortfolioGateway(
        UpvestInvestmentApiClient client,
        IOptions<UpvestSettings> settings,
        ILogger<UpvestPortfolioGateway> logger)
    {
        _client = client;
        _logger = logger;
        _budget = TimeSpan.FromSeconds(settings.Value.RequestTimeoutSeconds);
    }

    // ------------------------------------------------------------------ Flow 1: onboarding

    public async Task<InvestorOnboardingResult> OnboardInvestorAsync(
        InvestorSignUp form, InvestorOnboardingKeys keys, CancellationToken cancellationToken)
    {
        var nationality = ResolveNationality(form.Nationality);
        var addressCountry = ResolveCountry(form.Address.Country);
        var taxCountry = ResolveCountry(form.TaxCountry);
        var documentType = ResolveDocumentType(form.IdentityDocument.Type);
        var now = DateTimeOffset.UtcNow;

        var userBody = new UserTolCreateRequest
        {
            FirstName = form.FirstName,
            LastName = form.LastName,
            Email = form.Email,
            BirthDate = form.BirthDate,
            BirthCity = form.BirthCity,
            BirthCountry = ResolveBirthCountry(form.BirthCountry),
            Nationalities = new[] { nationality },
            PhoneNumber = string.IsNullOrWhiteSpace(form.PhoneNumber) ? null : form.PhoneNumber,
            Address = new Address2
            {
                AddressLine1 = form.Address.Line1,
                Postcode = form.Address.Postcode,
                City = form.Address.City,
                Country = addressCountry,
            },
            Fatca = new Fatca { Status = false, ConfirmedAt = now },
        };

        // Step 1 — create the user. Reconcile an unknown outcome by replaying the same idempotency key.
        Guid userId;
        try
        {
            var userResponse = await CallAsync(
                "CreateUser", isWrite: true,
                ct => _client.Users.CreateUser(
                    new CreateUserRequest { IdempotencyKey = keys.UserKey, Body = userBody }, cancellationToken: ct),
                cancellationToken).ConfigureAwait(false);
            userId = ExtractUserId(userResponse);
        }
        catch (UpvestGatewayException ex) when (ex.IsOutcomeUnknown)
        {
            _logger.LogWarning("CreateUser outcome unknown; replaying with the same idempotency key to reconcile.");
            var userResponse = await CallAsync(
                "CreateUser(replay)", isWrite: true,
                ct => _client.Users.CreateUser(
                    new CreateUserRequest { IdempotencyKey = keys.UserKey, Body = userBody }, cancellationToken: ct),
                cancellationToken).ConfigureAwait(false);
            userId = ExtractUserId(userResponse);
        }

        Guid? checkId = null;
        Guid? accountGroupId = null;
        Guid? accountId = null;
        string? incomplete = null;

        try
        {
            var checkBody = new UserCheckKnowYourCustomerCreateRequest
            {
                CheckConfirmedAt = now,
                DataDownloadLink = $"https://identity.eshoponweb.local/kyc-evidence/{userId:N}",
                DocumentType = documentType,
                DocumentExpirationDate = form.IdentityDocument.ExpiryDate,
                Nationality = form.IdentityDocument.IssuingCountry,
                Provider = KycProvider,
                Method = Method.ElectronicId,
            };
            var checkResponse = await CallAsync(
                "CreateUserCheck", isWrite: true,
                ct => _client.UserChecks.CreateUserCheck(
                    new CreateUserCheckRequest { UserId = userId, Body = checkBody }, cancellationToken: ct),
                cancellationToken).ConfigureAwait(false);
            checkId = checkResponse.Id;

            var taxBody = new TaxResidenciesSetRequest
            {
                TaxResidencies = new TaxResidencyForCreateRequest[]
                {
                    new WithTaxIdentifierNumber { Country = taxCountry, TaxIdentifierNumber = form.TaxId },
                },
            };
            await CallAsync(
                "SetTaxResidencies", isWrite: true,
                ct => _client.TaxResidencies.SetTaxResidencies(
                    new SetTaxResidenciesRequest { UserId = userId, IdempotencyKey = keys.TaxKey, Body = taxBody },
                    cancellationToken: ct),
                cancellationToken).ConfigureAwait(false);

            var groupResponse = await CallAsync(
                "CreateAccountGroup", isWrite: true,
                ct => _client.AccountGroups.CreateAccountGroup(
                    new CreateAccountGroupRequest
                    {
                        IdempotencyKey = keys.AccountGroupKey,
                        Body = new AccountGroupCreateUserRequest { UserId = userId, Type = Type13.Personal },
                    },
                    cancellationToken: ct),
                cancellationToken).ConfigureAwait(false);
            accountGroupId = ExtractAccountGroupId(groupResponse);

            var accountResponse = await CallAsync(
                "CreateAccount", isWrite: true,
                ct => _client.AccountsApi.CreateAccount(
                    new CreateAccountRequest
                    {
                        IdempotencyKey = keys.AccountKey,
                        Body = new AccountCreateUserRequest
                        {
                            UserId = userId,
                            AccountGroupId = accountGroupId!.Value,
                            Type = Type16.Portfolio,
                        },
                    },
                    cancellationToken: ct),
                cancellationToken).ConfigureAwait(false);
            accountId = ExtractAccountId(accountResponse);
        }
        catch (UpvestGatewayException ex) when (ex.IsAuthorizationScopeIssue)
        {
            // Expected in an environment whose Upvest client is authorised only for user operations.
            // The user exists; the rest of the chain is deferred, and the sign-up stays "pending".
            _logger.LogWarning(
                "Onboarding stopped at the scope boundary (status {Status}); investor will remain pending.",
                ex.StatusCode);
            incomplete = "Upvest client is not authorised beyond user operations in this environment.";
        }

        return new InvestorOnboardingResult(userId, checkId, accountGroupId, accountId, incomplete);
    }

    public async Task<InvestorStatus> GetInvestorStatusAsync(
        Guid userId, Guid? accountId, CancellationToken cancellationToken)
    {
        var userResponse = await CallAsync(
            "RetrieveUser", isWrite: false,
            ct => _client.Users.RetrieveUser(new RetrieveUserRequest { UserId = userId }, cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);

        var userStatus = ExtractUserStatus(userResponse);
        if (userStatus == Status.Offboarding || userStatus == Status.Offboarded)
        {
            return InvestorStatus.Rejected;
        }

        if (accountId is null)
        {
            // Onboarding has not reached account creation yet.
            return InvestorStatus.Pending;
        }

        var accountResponse = await CallAsync(
            "RetrieveAccount", isWrite: false,
            ct => _client.AccountsApi.RetrieveAccount(
                new RetrieveAccountRequest { AccountId = accountId.Value }, cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);

        var accountActive = accountResponse.TryGetAccount(out var account) && account.Status == Status21.Active;
        return userStatus == Status.Active && accountActive ? InvestorStatus.Active : InvestorStatus.Pending;
    }

    // ------------------------------------------------------------------ Flow 2: models

    public async Task<ModelPortfolioResult> CreateModelAsync(
        string name, IReadOnlyList<FundWeight> funds, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var body = new PortfoliosAllocationCreateRequest { Name = name, Allocation = BuildAllocation(funds) };
        var response = await CallAsync(
            "CreatePortfoliosAllocation", isWrite: true,
            ct => _client.Portfolios.CreatePortfoliosAllocation(
                new CreatePortfoliosAllocationRequest { IdempotencyKey = idempotencyKey, Body = body },
                cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);
        return MapAllocation(response);
    }

    public async Task<ModelPortfolioResult> ReplaceModelAsync(
        Guid allocationId, string name, IReadOnlyList<FundWeight> funds, CancellationToken cancellationToken)
    {
        var body = new PortfoliosAllocationUpdateRequest { Name = name, Allocation = BuildAllocation(funds) };
        var response = await CallAsync(
            "UpdatePortfoliosAllocation", isWrite: true,
            ct => _client.Portfolios.UpdatePortfoliosAllocation(
                new UpdatePortfoliosAllocationRequest { AllocationId = allocationId, Body = body },
                cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);
        return MapAllocation(response);
    }

    public async Task<IReadOnlyList<ModelPortfolioResult>> ListModelsAsync(CancellationToken cancellationToken)
    {
        const int pageSize = 100;
        const int maxPages = 100;
        var results = new List<ModelPortfolioResult>();
        int offset = 0;

        for (int page = 0; page < maxPages; page++)
        {
            int currentOffset = offset;
            var response = await CallAsync(
                "ListPortfoliosAllocations", isWrite: false,
                ct => _client.Portfolios.ListPortfoliosAllocations(
                    new ListPortfoliosAllocationsRequest { Offset = currentOffset, Limit = pageSize },
                    cancellationToken: ct),
                cancellationToken).ConfigureAwait(false);

            results.AddRange(response.Data.Select(MapAllocation));

            offset = response.Meta.Offset + response.Meta.Count;
            if (response.Meta.Count == 0 || offset >= response.Meta.TotalCount)
            {
                break;
            }
        }

        return results;
    }

    public async Task SetInvestorConfigurationAsync(
        Guid accountId, Guid allocationId, Guid idempotencyKey, bool alreadyConfigured, CancellationToken cancellationToken)
    {
        if (alreadyConfigured)
        {
            await CallAsync(
                "UpdatePortfoliosConfiguration", isWrite: true,
                ct => _client.Portfolios.UpdatePortfoliosConfiguration(
                    new UpdatePortfoliosConfigurationRequest
                    {
                        AccountId = accountId,
                        IdempotencyKey = idempotencyKey,
                        Body = new PortfoliosConfigurationUpdateRequest { AllocationId = allocationId },
                    },
                    cancellationToken: ct),
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await CallAsync(
                "CreatePortfoliosConfiguration", isWrite: true,
                ct => _client.Portfolios.CreatePortfoliosConfiguration(
                    new CreatePortfoliosConfigurationRequest
                    {
                        IdempotencyKey = idempotencyKey,
                        Body = new PortfoliosConfigurationCreateRequest { AccountId = accountId, AllocationId = allocationId },
                    },
                    cancellationToken: ct),
                cancellationToken).ConfigureAwait(false);
        }
    }

    // ------------------------------------------------------------------ Flow 3: paying in

    public async Task<PayInResult> PayInAsync(
        Guid userId,
        Guid accountId,
        Guid accountGroupId,
        Guid? existingVirtualBankAccountId,
        Guid virtualBankAccountIdempotencyKey,
        decimal amount,
        Guid orderIdempotencyKey,
        string clientReference,
        CancellationToken cancellationToken)
    {
        // The simulated SEPA credit must name the account owner. Read it from Upvest so personal data is
        // never persisted by this application.
        var ownerResponse = await CallAsync(
            "RetrieveUser", isWrite: false,
            ct => _client.Users.RetrieveUser(new RetrieveUserRequest { UserId = userId }, cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);
        var ownerName = ExtractOwnerName(ownerResponse);

        // Step 1 — ensure a virtual bank account exists for the account group (the sandbox credit target).
        Guid vibanId;
        if (existingVirtualBankAccountId is { } existing)
        {
            vibanId = existing;
        }
        else
        {
            var viban = await CallAsync(
                "CreateVirtualBankAccount", isWrite: true,
                ct => _client.VirtualBankAccounts.CreateVirtualBankAccount(
                    new CreateVirtualBankAccountRequest
                    {
                        IdempotencyKey = virtualBankAccountIdempotencyKey,
                        Body = new PaymentsVirtualBankAccountCreateRequest
                        {
                            AccountGroupId = accountGroupId,
                            Name = "eShopOnWeb managed portfolio",
                        },
                    },
                    cancellationToken: ct),
                cancellationToken).ConfigureAwait(false);
            vibanId = viban.Id;
        }

        // Step 2 — simulate an incoming SEPA credit so the account group has cash to invest (sandbox only).
        await CallAsync(
            "CreateBankTransaction", isWrite: true,
            ct => _client.Tests.CreateBankTransaction(
                new CreateBankTransactionRequest
                {
                    IdempotencyKey = orderIdempotencyKey,
                    Body = new PaymentsBankTransactionCreateRequest
                    {
                        Account = new Account29 { Iban = SandboxPoolingIban },
                        Amount = FormatMoney(amount),
                        Currency = BankTransactionCurrency.Eur,
                        ClientReference = clientReference,
                        CounterpartyName = ownerName,
                        CounterpartyAccount = new CounterpartyAccount { Iban = SandboxCounterpartyIban },
                        VirtualBankAccountId = vibanId,
                    },
                },
                cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);

        // Step 3 — invest the cash across the model's funds (a BUY portfolio order).
        var order = await CallAsync(
            "CreatePortfoliosOrder", isWrite: true,
            ct => _client.Portfolios.CreatePortfoliosOrder(
                new CreatePortfoliosOrderRequest
                {
                    IdempotencyKey = orderIdempotencyKey,
                    Body = new PortfoliosOrderPlaceRequest
                    {
                        UserId = userId,
                        AccountId = accountId,
                        CashAmount = FormatMoney(amount),
                        Currency = Currency.Eur,
                        Side = Side15.Buy,
                    },
                },
                cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);

        return new PayInResult(order.Id, MapOrderStatus(order.Status), vibanId);
    }

    public async Task<PaymentStatus> GetPaymentStatusAsync(Guid portfolioOrderId, CancellationToken cancellationToken)
    {
        var order = await CallAsync(
            "RetrievePortfoliosOrder", isWrite: false,
            ct => _client.Portfolios.RetrievePortfoliosOrder(
                new RetrievePortfoliosOrderRequest { PortfolioOrderId = portfolioOrderId }, cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);
        return MapOrderStatus(order.Status);
    }

    // ------------------------------------------------------------------ Flow 4: realignment

    public async Task<Guid> TriggerRealignmentAsync(
        Guid allocationId, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var response = await CallAsync(
            "TriggerPortfolioRebalancing", isWrite: true,
            ct => _client.PortfoliosRebalancing.TriggerPortfolioRebalancing(
                new TriggerPortfolioRebalancingOperationRequest
                {
                    IdempotencyKey = idempotencyKey,
                    Body = TriggerPortfolioRebalancingRequest.Allocations(
                        new Allocations { AllocationsValue = new[] { allocationId } }),
                },
                cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);
        return response.Id;
    }

    public async Task<RealignmentStatus> GetRealignmentStatusAsync(Guid executionId, CancellationToken cancellationToken)
    {
        // The rebalancing execution object carries no status; derive it from its execution orders.
        const int pageSize = 100;
        const int maxPages = 100;
        var statuses = new List<Status71>();
        int offset = 0;

        for (int page = 0; page < maxPages; page++)
        {
            int currentOffset = offset;
            var response = await CallAsync(
                "ListPortfolioRebalancingExecutionOrders", isWrite: false,
                ct => _client.PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders(
                    new ListPortfolioRebalancingExecutionOrdersRequest
                    {
                        ExecutionId = executionId,
                        Offset = currentOffset,
                        Limit = pageSize,
                    },
                    cancellationToken: ct),
                cancellationToken).ConfigureAwait(false);

            statuses.AddRange(response.Data.Select(o => o.Status));

            offset = response.Meta.Offset + response.Meta.Count;
            if (response.Meta.Count == 0 || offset >= response.Meta.TotalCount)
            {
                break;
            }
        }

        if (statuses.Count == 0)
        {
            return RealignmentStatus.Pending; // orders not produced yet
        }

        if (statuses.Any(s => s == Status71.New || s == Status71.Processing))
        {
            return RealignmentStatus.Pending;
        }

        if (statuses.All(s => s == Status71.Filled))
        {
            return RealignmentStatus.Completed;
        }

        // Nothing still processing, and not all filled ⇒ at least one cancelled.
        return RealignmentStatus.Failed;
    }

    // ------------------------------------------------------------------ Flow 5: portfolio

    public async Task<PortfolioSnapshot> GetPortfolioAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var valuation = await CallAsync(
            "GetAccountValuation", isWrite: false,
            ct => _client.Valuations.GetAccountValuation(
                new GetAccountValuationRequest { AccountId = accountId, PriceQuality = PriceQuality3.HighestAvailable },
                cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);

        var holdings = valuation.SecurityPositions.Select(p => new Holding(
            InstrumentId: p.Instrument.Isin ?? p.Instrument.Uuid.ToString(),
            Quantity: ParseDecimal(p.Quantity),
            Value: p.Value is null ? 0m : ParseDecimal(p.Value.Amount))).ToList();

        return new PortfolioSnapshot(holdings, ParseDecimal(valuation.TotalSecurityValue.Amount));
    }

    // ------------------------------------------------------------------ helpers

    private async Task<T> CallAsync<T>(
        string operation, bool isWrite, Func<CancellationToken, Task<T>> call, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_budget);

        try
        {
            _logger.LogInformation("Upvest call {Operation} starting.", operation);
            var result = await call(cts.Token).ConfigureAwait(false);
            _logger.LogInformation("Upvest call {Operation} succeeded.", operation);
            return result;
        }
        catch (ResponseDeserializationException ex)
        {
            int status = (int)ex.StatusCode;
            bool unknown = isWrite && status is >= 200 and < 300;
            _logger.LogWarning("Upvest call {Operation} returned an unreadable body (status {Status}).", operation, status);
            throw new UpvestGatewayException(
                "Upvest returned a response that could not be processed.", status, isOutcomeUnknown: unknown, innerException: ex);
        }
        catch (ApiException ex)
        {
            int status = (int)ex.StatusCode;
            bool scope = status is 401 or 403;
            _logger.LogWarning("Upvest call {Operation} failed with status {Status}.", operation, status);
            throw new UpvestGatewayException(
                scope ? "Upvest rejected the request's authorisation." : $"Upvest rejected the {operation} request.",
                status, isAuthorizationScopeIssue: scope, innerException: ex);
        }
        catch (SdkTimeoutException ex)
        {
            _logger.LogWarning("Upvest call {Operation} timed out.", operation);
            throw new UpvestGatewayException("Upvest did not respond in time.", isOutcomeUnknown: isWrite, innerException: ex);
        }
        catch (SdkConnectionException ex)
        {
            _logger.LogWarning("Upvest call {Operation} could not reach the provider.", operation);
            throw new UpvestGatewayException("Upvest is unreachable.", isOutcomeUnknown: isWrite, innerException: ex);
        }
        catch (AuthSchemeException ex)
        {
            // Our credential is missing/could not be applied — never the caller's fault; log the cause only.
            _logger.LogError(ex, "Upvest call {Operation} failed to apply credentials.", operation);
            throw new UpvestGatewayException(
                "Upvest credentials are not configured correctly.", isAuthorizationScopeIssue: true, innerException: ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // the caller cancelled — not a provider failure
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning("Upvest call {Operation} exceeded its budget.", operation);
            throw new UpvestGatewayException("Upvest did not respond in time.", isOutcomeUnknown: isWrite, innerException: ex);
        }
    }

    private static IReadOnlyList<Allocation> BuildAllocation(IReadOnlyList<FundWeight> funds) =>
        funds.Select(f => new Allocation
        {
            InstrumentId = f.InstrumentId, // implicit string -> InstrumentId
            InstrumentIdType = InstrumentIdType4.Isin,
            Weight = FormatWeight(f.Weight),
        }).ToList();

    private static ModelPortfolioResult MapAllocation(PortfoliosAllocation a) => new(
        a.Id,
        a.Name,
        a.Allocation.Select(x => new FundWeight(
            x.InstrumentId.TryGetString(out var isin) ? isin : x.InstrumentId.ToString(),
            ParseDecimal(x.Weight))).ToList());

    private static PaymentStatus MapOrderStatus(Status65 status)
    {
        if (status == Status65.Filled || status == Status65.Settled) return PaymentStatus.Invested;
        if (status == Status65.Cancelled) return PaymentStatus.Failed;
        return PaymentStatus.Pending; // New, Processing
    }

    private static Guid ExtractUserId(UserCreateRequest1 response)
    {
        if (response.TryGetUserTol(out var tol)) return tol.Id;
        if (response.TryGetUserByol(out var byol)) return byol.Id;
        throw new UpvestGatewayException("Upvest returned an unrecognised user payload.");
    }

    private static string ExtractOwnerName(UserGetResponse response)
    {
        string raw;
        if (response.TryGetUserTol(out var tol)) raw = $"{tol.FirstName} {tol.LastName}";
        else if (response.TryGetUserByol(out var byol)) raw = $"{byol.FirstName} {byol.LastName}";
        else throw new UpvestGatewayException("Upvest returned an unrecognised user payload.");

        // Keep only characters the counterparty_name field accepts; fall back to a safe placeholder.
        var cleaned = new string(raw.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '.' or '\'').ToArray()).Trim();
        return string.IsNullOrEmpty(cleaned) ? "Investor" : cleaned;
    }

    private static Status ExtractUserStatus(UserGetResponse response)
    {
        if (response.TryGetUserTol(out var tol)) return tol.Status;
        if (response.TryGetUserByol(out var byol)) return byol.Status;
        throw new UpvestGatewayException("Upvest returned an unrecognised user payload.");
    }

    private static Guid ExtractAccountGroupId(AccountGroupCreateResponse response)
    {
        if (response.TryGetAccountGroup(out var ag)) return ag.Id;
        if (response.TryGetBusinessAccountGroup(out var bag)) return bag.Id;
        throw new UpvestGatewayException("Upvest returned an unrecognised account-group payload.");
    }

    private static Guid ExtractAccountId(AccountCreateResponse response)
    {
        if (response.TryGetAccount(out var a)) return a.Id;
        if (response.TryGetBusinessAccount(out var ba)) return ba.Id;
        throw new UpvestGatewayException("Upvest returned an unrecognised account payload.");
    }

    private static Nationality ResolveNationality(string code)
    {
        if (Nationality.TryGetKnownValue(code, out var v)) return v;
        throw new UpvestGatewayException($"Unsupported nationality code '{Sanitize(code)}'.", 400);
    }

    private static Country ResolveCountry(string code)
    {
        if (Country.TryGetKnownValue(code, out var v)) return v;
        throw new UpvestGatewayException($"Unsupported country code '{Sanitize(code)}'.", 400);
    }

    private static BirthCountry ResolveBirthCountry(string code)
    {
        if (BirthCountry.TryGetKnownValue(code, out var v)) return v;
        throw new UpvestGatewayException($"Unsupported birth country code '{Sanitize(code)}'.", 400);
    }

    private static DocumentType3 ResolveDocumentType(string type)
    {
        if (DocumentType3.TryGetKnownValue(type, out var v)) return v;
        throw new UpvestGatewayException($"Unsupported identity document type '{Sanitize(type)}'.", 400);
    }

    private static string Sanitize(string value) =>
        string.IsNullOrEmpty(value) ? "(empty)" : new string(value.Where(char.IsLetterOrDigit).Take(8).ToArray());

    private static string FormatMoney(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatWeight(decimal weight) =>
        weight == 100m ? "100" : weight.ToString("0.#####", CultureInfo.InvariantCulture);

    private static decimal ParseDecimal(string value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
}
