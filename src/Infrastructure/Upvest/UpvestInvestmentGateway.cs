using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
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
using UpvestInvestmentApi.Requests.TopUps;
using UpvestInvestmentApi.Requests.UserIdentifiers;
using UpvestInvestmentApi.Requests.Users;
using UpvestInvestmentApi.Requests.Valuations;
using UpvestInvestmentApi.Requests.VirtualBankAccounts;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The only code that talks to Upvest. Translates the app's domain operations to Upvest SDK calls and SDK
/// failures to <see cref="UpvestGatewayException"/>. No personal data is ever logged here.
/// </summary>
public sealed class UpvestInvestmentGateway : IUpvestInvestmentGateway
{
    // A total per-call budget across SDK retries, bounding what the app's own caller waits for.
    private static readonly TimeSpan CallBudget = TimeSpan.FromSeconds(30);

    // A made-up external counterparty for the sandbox settlement transfer (never a real person).
    private const string SandboxCounterpartyName = "eShopOnWeb Funding";
    private const string SandboxCounterpartyIban = "DE89370400440532013000";

    private readonly UpvestInvestmentApiClient _client;
    private readonly IAppLogger<UpvestInvestmentGateway> _logger;

    public UpvestInvestmentGateway(UpvestInvestmentApiClient client, IAppLogger<UpvestInvestmentGateway> logger)
    {
        _client = client;
        _logger = logger;
    }

    // ----- Flow 1 -----------------------------------------------------------------------------------

    public async Task<InvestorProvisioning> CreateInvestorAsync(InvestorSignup signup, InvestorProvisioningKeys keys, CancellationToken ct)
    {
        var body = new UserTolCreateRequest
        {
            FirstName = signup.FirstName,
            LastName = signup.LastName,
            Email = signup.Email,
            BirthDate = new DateTimeOffset(signup.BirthDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            Nationalities = new[] { UpvestWire.Nationality(signup.Nationality) },
            Address = new Address2
            {
                AddressLine1 = signup.Address.Line1,
                Postcode = signup.Address.Postcode,
                City = signup.Address.City,
                Country = UpvestWire.Country(signup.Address.Country)
            },
            Fatca = new Fatca { Status = false, ConfirmedAt = DateTimeOffset.UtcNow },
            BirthCity = signup.BirthCity,
            BirthCountry = UpvestWire.BirthCountry(signup.BirthCountry),
            PhoneNumber = string.IsNullOrWhiteSpace(signup.PhoneNumber) ? null : signup.PhoneNumber
        };

        var created = await CallAsync(
            c => _client.Users.CreateUser(new CreateUserRequest
            {
                IdempotencyKey = keys.UserKey,
                Body = UserCreateRequest.UserTolCreateRequest(body)
            }, cancellationToken: c),
            "CreateUser", ct, isWrite: true);

        var userId = created.Match(
            onUserByol: u => u.Id,
            onUserTol: u => u.Id,
            otherwise: _ => throw new UpvestGatewayException("Upvest returned an unrecognised user response.", HttpStatusCode.BadGateway));

        _logger.LogInformation("Upvest user {UserId} created.", userId);

        // KYC the shop collected — submitted on a best-effort basis (these resources need scopes the
        // sandbox client may not hold). A failure here never blocks becoming an investor.
        await SubmitIdentityDocumentAsync(userId, signup.IdentityDocument, ct);
        await SubmitTaxResidencyAsync(userId, signup, keys, ct);

        return await ProvisionAccountAsync(userId, null, null, keys, ct);
    }

    public async Task<InvestorProvisioning> ProvisionAccountAsync(Guid userId, Guid? accountGroupId, Guid? accountId, InvestorProvisioningKeys keys, CancellationToken ct)
    {
        try
        {
            if (accountGroupId is null)
            {
                var ag = await CallAsync(
                    c => _client.AccountGroups.CreateAccountGroup(new CreateAccountGroupRequest
                    {
                        IdempotencyKey = keys.AccountGroupKey,
                        Body = AccountGroupCreateRequest.AccountGroupCreateUserRequest(new AccountGroupCreateUserRequest
                        {
                            UserId = userId,
                            Type = Type13.Personal
                        })
                    }, cancellationToken: c),
                    "CreateAccountGroup", ct, isWrite: true);

                accountGroupId = ag.Match(
                    onAccountGroup: g => (Guid?)g.Id,
                    onBusinessAccountGroup: _ => null,
                    otherwise: _ => null);
                _logger.LogInformation("Upvest account group {AccountGroupId} created.", accountGroupId);
            }

            if (accountGroupId is not null && accountId is null)
            {
                var ac = await CallAsync(
                    c => _client.AccountsApi.CreateAccount(new CreateAccountRequest
                    {
                        IdempotencyKey = keys.AccountKey,
                        Body = AccountCreateRequest.AccountCreateUserRequest(new AccountCreateUserRequest
                        {
                            UserId = userId,
                            AccountGroupId = accountGroupId.Value,
                            Type = Type16.Portfolio,
                            Name = "Managed portfolio"
                        })
                    }, cancellationToken: c),
                    "CreateAccount", ct, isWrite: true);

                accountId = ac.Match(
                    onAccount: a => (Guid?)a.Id,
                    onBusinessAccount: _ => null,
                    otherwise: _ => null);
                _logger.LogInformation("Upvest account {AccountId} created.", accountId);
            }
        }
        catch (UpvestGatewayException ex)
        {
            // Provisioning could not complete now (e.g. the sandbox scope wall). The ids obtained so far
            // are kept and the status reflects how far we got; a later read self-heals the rest.
            _logger.LogWarning("Account provisioning incomplete (status {Status}).", ex.StatusCode);
        }

        var status = await DeriveInvestorStatusAsync(userId, accountGroupId, accountId, ct);
        return new InvestorProvisioning(userId, accountGroupId, accountId, status);
    }

    public async Task<InvestorStatusInfo> GetInvestorStatusAsync(Guid userId, Guid? accountGroupId, Guid? accountId, CancellationToken ct)
    {
        var status = await DeriveInvestorStatusAsync(userId, accountGroupId, accountId, ct);
        return new InvestorStatusInfo(status);
    }

    private async Task<InvestorStatus> DeriveInvestorStatusAsync(Guid userId, Guid? accountGroupId, Guid? accountId, CancellationToken ct)
    {
        string? userStatus = null;
        try
        {
            var userResp = await CallAsync(
                c => _client.Users.RetrieveUser(new RetrieveUserRequest { UserId = userId }, cancellationToken: c),
                "RetrieveUser", ct);
            userStatus = userResp.Match(
                onUserByol: u => u.Status.Value,
                onUserTol: u => u.Status.Value,
                otherwise: _ => null);
        }
        catch (UpvestGatewayException ex)
        {
            _logger.LogWarning("Could not read user status (status {Status}).", ex.StatusCode);
            return InvestorStatus.Pending;
        }

        if (userStatus is "OFFBOARDING" or "OFFBOARDED")
        {
            return InvestorStatus.Rejected;
        }

        if (accountGroupId is not null && accountId is not null)
        {
            try
            {
                var groupResp = await CallAsync(
                    c => _client.AccountGroups.RetrieveAccountGroup(new RetrieveAccountGroupRequest { AccountGroupId = accountGroupId.Value }, cancellationToken: c),
                    "RetrieveAccountGroup", ct);
                var groupStatus = groupResp.Match(
                    onAccountGroup: g => g.Status.Value,
                    onBusinessAccountGroup: g => g.Status.Value,
                    otherwise: _ => null);
                if (userStatus == "ACTIVE" && groupStatus == "ACTIVE")
                {
                    return InvestorStatus.Active;
                }
            }
            catch (UpvestGatewayException ex)
            {
                _logger.LogWarning("Could not read account group status (status {Status}).", ex.StatusCode);
            }
        }

        return InvestorStatus.Pending;
    }

    private async Task SubmitIdentityDocumentAsync(Guid userId, InvestorIdentityDocument document, CancellationToken ct)
    {
        var type = document.Type?.Trim().ToUpperInvariant() is "PASSPORT" ? Type7.Passport : Type7.NationalId;
        var issuingCountry = UpvestWire.IssuingCountry(document.IssuingCountry);
        try
        {
            await CallAsync(
                c => _client.UserIdentifiers.CreateIdentifier(new CreateIdentifierRequest
                {
                    UserId = userId,
                    Body = new IdentifierCreateRequest
                    {
                        Type = type,
                        IssuingCountry = issuingCountry,
                        Identifier = document.Number
                    }
                }, cancellationToken: c),
                "CreateIdentifier", ct, isWrite: true);
            _logger.LogInformation("Identity document submitted for user {UserId}.", userId);
        }
        catch (UpvestGatewayException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            // Some jurisdictions use a CONCAT identifier that Upvest derives from the user's name and
            // date of birth; in that case the explicit value is rejected. Retry letting Upvest generate it.
            try
            {
                await CallAsync(
                    c => _client.UserIdentifiers.CreateIdentifier(new CreateIdentifierRequest
                    {
                        UserId = userId,
                        Body = new IdentifierCreateRequest { Type = type, IssuingCountry = issuingCountry }
                    }, cancellationToken: c),
                    "CreateIdentifier", ct, isWrite: true);
                _logger.LogInformation("Identity document recorded (provider-generated identifier) for user {UserId}.", userId);
            }
            catch (UpvestGatewayException retryEx)
            {
                _logger.LogWarning("Identity document submission skipped for user {UserId} (status {Status}).", userId, retryEx.StatusCode);
            }
        }
        catch (UpvestGatewayException ex)
        {
            _logger.LogWarning("Identity document submission skipped for user {UserId} (status {Status}).", userId, ex.StatusCode);
        }
    }

    private async Task SubmitTaxResidencyAsync(Guid userId, InvestorSignup signup, InvestorProvisioningKeys keys, CancellationToken ct)
    {
        try
        {
            await CallAsync(
                c => _client.TaxResidencies.SetTaxResidencies(new SetTaxResidenciesRequest
                {
                    UserId = userId,
                    IdempotencyKey = keys.UserKey,
                    Body = new TaxResidenciesSetRequest
                    {
                        TaxResidencies = new[]
                        {
                            TaxResidencyForCreateRequest.WithTaxIdentifierNumber(new WithTaxIdentifierNumber
                            {
                                Country = UpvestWire.Country(signup.TaxCountry),
                                TaxIdentifierNumber = signup.TaxId
                            })
                        }
                    }
                }, cancellationToken: c),
                "SetTaxResidencies", ct, isWrite: true);
            _logger.LogInformation("Tax residency submitted for user {UserId}.", userId);
        }
        catch (UpvestGatewayException ex)
        {
            _logger.LogWarning("Tax residency submission skipped for user {UserId} (status {Status}).", userId, ex.StatusCode);
        }
    }

    // ----- Flow 2 -----------------------------------------------------------------------------------

    public async Task<IReadOnlySet<string>> ListTradeableIsinsAsync(CancellationToken ct)
    {
        var isins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        const int pageSize = 100;
        const int maxPages = 200;
        int offset = 0;

        for (var page = 0; page < maxPages; page++)
        {
            var resp = await CallAsync(
                c => _client.Instruments.ListInstruments(new ListInstrumentsRequest
                {
                    TradingStatus = InstrumentTradingStatus.Active,
                    Offset = offset,
                    Limit = pageSize
                }, cancellationToken: c),
                "ListInstruments", ct);

            foreach (var item in resp.Data)
            {
                if (!string.IsNullOrWhiteSpace(item.Isin))
                {
                    isins.Add(item.Isin!);
                }
            }

            offset += resp.Meta.Count;
            if (resp.Meta.Count == 0 || offset >= resp.Meta.TotalCount)
            {
                break;
            }
        }

        return isins;
    }

    public async Task<Guid> CreateModelAllocationAsync(string name, IReadOnlyList<FundWeight> funds, Guid idempotencyKey, CancellationToken ct)
    {
        var allocation = await CallAsync(
            c => _client.Portfolios.CreatePortfoliosAllocation(new CreatePortfoliosAllocationRequest
            {
                IdempotencyKey = idempotencyKey,
                Body = new PortfoliosAllocationCreateRequest
                {
                    Name = name,
                    Allocation = BuildAllocation(funds)
                }
            }, cancellationToken: c),
            "CreatePortfoliosAllocation", ct, isWrite: true);
        return allocation.Id;
    }

    public async Task UpdateModelAllocationAsync(Guid allocationId, string name, IReadOnlyList<FundWeight> funds, CancellationToken ct)
    {
        await CallAsync(
            c => _client.Portfolios.UpdatePortfoliosAllocation(new UpdatePortfoliosAllocationRequest
            {
                AllocationId = allocationId,
                Body = new PortfoliosAllocationUpdateRequest
                {
                    Name = name,
                    Allocation = BuildAllocation(funds)
                }
            }, cancellationToken: c),
            "UpdatePortfoliosAllocation", ct, isWrite: true);
    }

    public async Task SetFollowedModelAsync(Guid accountId, Guid allocationId, Guid idempotencyKey, CancellationToken ct)
    {
        // Does a configuration already exist for this account? If so, switch it; otherwise create it.
        bool exists;
        try
        {
            await CallAsync(
                c => _client.Portfolios.RetrievePortfoliosConfiguration(new RetrievePortfoliosConfigurationRequest { AccountId = accountId }, cancellationToken: c),
                "RetrievePortfoliosConfiguration", ct);
            exists = true;
        }
        catch (UpvestGatewayException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            exists = false;
        }

        if (exists)
        {
            await CallAsync(
                c => _client.Portfolios.UpdatePortfoliosConfiguration(new UpdatePortfoliosConfigurationRequest
                {
                    AccountId = accountId,
                    IdempotencyKey = idempotencyKey,
                    Body = new PortfoliosConfigurationUpdateRequest { AllocationId = allocationId }
                }, cancellationToken: c),
                "UpdatePortfoliosConfiguration", ct, isWrite: true);
        }
        else
        {
            await CallAsync(
                c => _client.Portfolios.CreatePortfoliosConfiguration(new CreatePortfoliosConfigurationRequest
                {
                    IdempotencyKey = idempotencyKey,
                    Body = new PortfoliosConfigurationCreateRequest { AccountId = accountId, AllocationId = allocationId }
                }, cancellationToken: c),
                "CreatePortfoliosConfiguration", ct, isWrite: true);
        }
    }

    private static List<Allocation> BuildAllocation(IReadOnlyList<FundWeight> funds) =>
        funds.Select(f => new Allocation
        {
            InstrumentId = InstrumentId.String(f.InstrumentId),
            InstrumentIdType = InstrumentIdType4.Isin,
            Weight = UpvestWire.Weight(f.Weight)
        }).ToList();

    // ----- Flow 3 -----------------------------------------------------------------------------------

    public async Task<PayInResult> PayInAsync(PayInCommand command, CancellationToken ct)
    {
        var amount = UpvestWire.Money2Dp(command.AmountEuros);

        // 1. Find the account group's virtual bank account (the IBAN the top-up settles into).
        var destinationIban = await GetVirtualIbanAsync(command.AccountGroupId, ct);

        // 2. Create the top-up (credits the account-group cash balance when settled).
        var topup = await CallAsync(
            c => _client.TopUps.CreateTopup(new CreateTopupRequest
            {
                IdempotencyKey = command.TopupKey,
                Body = new PaymentsTopUpCreateRequest
                {
                    AccountGroupId = command.AccountGroupId,
                    CashAmount = amount,
                    Currency = Currency.Eur
                }
            }, cancellationToken: c),
            "CreateTopup", ct, isWrite: true);

        var topupId = topup.Id;
        _logger.LogInformation("Top-up {TopupId} created.", topupId);

        // 3. Settle the top-up in the sandbox via a simulated incoming bank transaction.
        if (!string.IsNullOrWhiteSpace(destinationIban))
        {
            await CallAsync(
                c => _client.Tests.CreateBankTransaction(new CreateBankTransactionRequest
                {
                    IdempotencyKey = command.BankTransactionKey,
                    Body = new PaymentsBankTransactionCreateRequest
                    {
                        Account = new Account29 { Iban = destinationIban! },
                        Amount = amount,
                        Currency = BankTransactionCurrency.Eur,
                        ClientReference = command.ClientReference,
                        CounterpartyName = SandboxCounterpartyName,
                        CounterpartyAccount = new CounterpartyAccount { Iban = SandboxCounterpartyIban },
                        RemittanceInformation = topup.SettlementReference
                    }
                }, cancellationToken: c),
                "CreateBankTransaction", ct, isWrite: true);
            _logger.LogInformation("Sandbox settlement submitted for top-up {TopupId}.", topupId);
        }

        // 4. Invest the cash across the followed model (portfolio BUY order).
        var order = await CallAsync(
            c => _client.Portfolios.CreatePortfoliosOrder(new CreatePortfoliosOrderRequest
            {
                IdempotencyKey = command.OrderKey,
                Body = new PortfoliosOrderPlaceRequest
                {
                    UserId = command.UserId,
                    AccountId = command.AccountId,
                    CashAmount = amount,
                    Currency = Currency.Eur,
                    Side = Side15.Buy
                }
            }, cancellationToken: c),
            "CreatePortfoliosOrder", ct, isWrite: true);

        _logger.LogInformation("Portfolio order {OrderId} created; status {Status}.", order.Id, order.Status.Value);
        return new PayInResult(topupId, order.Id, MapOrderStatus(order.Status.Value));
    }

    public async Task<ApplicationCore.Entities.PortfolioAggregate.PaymentStatus> GetPaymentStatusAsync(Guid? topupId, Guid? orderId, CancellationToken ct)
    {
        if (orderId is not null)
        {
            var order = await CallAsync(
                c => _client.Portfolios.RetrievePortfoliosOrder(new RetrievePortfoliosOrderRequest { PortfolioOrderId = orderId.Value }, cancellationToken: c),
                "RetrievePortfoliosOrder", ct);
            return MapOrderStatus(order.Status.Value);
        }

        if (topupId is not null)
        {
            var topup = await CallAsync(
                c => _client.TopUps.RetrieveTopup(new RetrieveTopupRequest { TopupId = topupId.Value }, cancellationToken: c),
                "RetrieveTopup", ct);
            return topup.Status?.Value == "CANCELLED" ? PaymentStatus.Failed : PaymentStatus.Pending;
        }

        return PaymentStatus.Pending;
    }

    private async Task<string?> GetVirtualIbanAsync(Guid accountGroupId, CancellationToken ct)
    {
        var resp = await CallAsync(
            c => _client.VirtualBankAccounts.ListVirtualBankAccounts(new ListVirtualBankAccountsRequest { AccountGroupId = accountGroupId }, cancellationToken: c),
            "ListVirtualBankAccounts", ct);
        return resp.Data.Select(v => v.Identification.Swift?.Iban).FirstOrDefault(iban => !string.IsNullOrWhiteSpace(iban));
    }

    private static PaymentStatus MapOrderStatus(string status) => status switch
    {
        "FILLED" or "SETTLED" => PaymentStatus.Invested,
        "CANCELLED" => PaymentStatus.Failed,
        _ => PaymentStatus.Pending
    };

    // ----- Flow 4 -----------------------------------------------------------------------------------

    public async Task<Guid> TriggerRealignmentAsync(Guid allocationId, Guid idempotencyKey, CancellationToken ct)
    {
        var resp = await CallAsync(
            c => _client.PortfoliosRebalancing.TriggerPortfolioRebalancing(new TriggerPortfolioRebalancingOperationRequest
            {
                IdempotencyKey = idempotencyKey,
                Body = TriggerPortfolioRebalancingRequest.Allocations(new Allocations
                {
                    AllocationsValue = new[] { allocationId }
                })
            }, cancellationToken: c),
            "TriggerPortfolioRebalancing", ct, isWrite: true);
        return resp.Id;
    }

    public async Task<RealignmentStatus> GetRealignmentStatusAsync(Guid executionId, CancellationToken ct)
    {
        // The execution resource itself carries no status; derive it from its execution orders.
        var statuses = new List<string>();
        const int pageSize = 100;
        const int maxPages = 200;
        int offset = 0;

        for (var page = 0; page < maxPages; page++)
        {
            var resp = await CallAsync(
                c => _client.PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders(new ListPortfolioRebalancingExecutionOrdersRequest
                {
                    ExecutionId = executionId,
                    Offset = offset,
                    Limit = pageSize
                }, cancellationToken: c),
                "ListPortfolioRebalancingExecutionOrders", ct);

            statuses.AddRange(resp.Data.Select(o => o.Status.Value));
            offset += resp.Meta.Count;
            if (resp.Meta.Count == 0 || offset >= resp.Meta.TotalCount)
            {
                break;
            }
        }

        return DeriveRealignmentStatus(statuses);
    }

    private static RealignmentStatus DeriveRealignmentStatus(IReadOnlyList<string> orderStatuses)
    {
        if (orderStatuses.Count == 0)
        {
            return RealignmentStatus.Pending; // not yet broken down into orders
        }
        if (orderStatuses.Any(s => s is "NEW" or "PROCESSING"))
        {
            return RealignmentStatus.Pending;
        }
        if (orderStatuses.All(s => s == "FILLED"))
        {
            return RealignmentStatus.Completed;
        }
        // All terminal, but at least one not FILLED (e.g. CANCELLED).
        return RealignmentStatus.Failed;
    }

    // ----- Flow 5 -----------------------------------------------------------------------------------

    public async Task<PortfolioSnapshot> GetPortfolioAsync(Guid accountId, CancellationToken ct)
    {
        Guid? allocationId = null;
        try
        {
            var config = await CallAsync(
                c => _client.Portfolios.RetrievePortfoliosConfiguration(new RetrievePortfoliosConfigurationRequest { AccountId = accountId }, cancellationToken: c),
                "RetrievePortfoliosConfiguration", ct);
            allocationId = config.AllocationId;
        }
        catch (UpvestGatewayException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // No model followed yet.
        }

        var valuation = await CallAsync(
            c => _client.Valuations.GetAccountValuation(new GetAccountValuationRequest
            {
                AccountId = accountId,
                PriceQuality = PriceQuality3.HighestAvailable
            }, cancellationToken: c),
            "GetAccountValuation", ct);

        var holdings = valuation.SecurityPositions
            .Select(p => new PortfolioHolding(
                p.Instrument.Isin ?? p.Instrument.Uuid.ToString(),
                UpvestWire.ParseDecimal(p.Quantity),
                UpvestWire.ParseDecimal(p.Value?.Amount)))
            .ToList();

        var totalValue = UpvestWire.ParseDecimal(valuation.TotalSecurityValue.Amount);
        return new PortfolioSnapshot(allocationId, holdings, totalValue);
    }

    // ----- SDK call boundary ------------------------------------------------------------------------

    private async Task<T> CallAsync<T>(Func<CancellationToken, Task<T>> operation, string action, CancellationToken ct, bool isWrite = false)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(CallBudget);
        try
        {
            return await operation(cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new UpvestGatewayException($"The investment provider did not respond in time ({action}).", null, null, outcomeUnknown: isWrite);
        }
        catch (ResponseDeserializationException ex)
        {
            // A 2xx that could not be read leaves a write's outcome unknown; a non-2xx keeps its status.
            var unknown = isWrite && IsSuccess(ex.StatusCode);
            throw new UpvestGatewayException("The investment provider returned a response that could not be processed.", ex.StatusCode, ex, unknown);
        }
        catch (ApiException ex)
        {
            throw new UpvestGatewayException($"The investment provider rejected the request ({action}).", ex.StatusCode, ex);
        }
        catch (SdkTimeoutException ex)
        {
            throw new UpvestGatewayException($"The investment provider did not respond in time ({action}).", null, ex, isWrite);
        }
        catch (SdkConnectionException ex)
        {
            throw new UpvestGatewayException($"The investment provider is unreachable ({action}).", null, ex, isWrite);
        }
        catch (AuthSchemeException ex)
        {
            throw new UpvestGatewayException("The investment provider credentials are missing or could not be applied.", null, ex);
        }
    }

    private async Task CallAsync(Func<CancellationToken, Task> operation, string action, CancellationToken ct, bool isWrite = false)
    {
        await CallAsync<bool>(async c => { await operation(c); return true; }, action, ct, isWrite);
    }

    private static bool IsSuccess(HttpStatusCode status) => (int)status is >= 200 and < 300;
}
