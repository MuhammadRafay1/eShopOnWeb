using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
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
using UpvestInvestmentApi.Requests.TopUps;
using UpvestInvestmentApi.Requests.UserIdentifiers;
using UpvestInvestmentApi.Requests.Users;
using UpvestInvestmentApi.Requests.Valuations;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Implements <see cref="IUpvestInvestmentService"/> over the Upvest Investment API .NET SDK. Every SDK call
/// is wrapped so that any failure becomes a <see cref="UpvestApiException"/> carrying a caller-safe message and
/// (when the provider answered) the HTTP status; connection/timeout failures on writes are marked
/// <see cref="UpvestApiException.OutcomeUnknown"/>. No personal data is ever logged.
/// </summary>
public class UpvestInvestmentService : IUpvestInvestmentService
{
    private readonly UpvestInvestmentApiClient _client;
    private readonly ILogger<UpvestInvestmentService> _logger;

    public UpvestInvestmentService(UpvestInvestmentApiClient client, ILogger<UpvestInvestmentService> logger)
    {
        _client = client;
        _logger = logger;
    }

    // ---------------------------------------------------------------- Flow 1: investor provisioning

    public async Task<InvestorProvisioningResult> ProvisionInvestorAsync(string buyerId, InvestorSignUp s, CancellationToken ct)
    {
        // 1) Create the user (BYOL — the shop verified the shopper's identity itself).
        var userBody = new UserByolCreateRequest
        {
            FirstName = s.FirstName,
            LastName = s.LastName,
            Email = s.Email,
            BirthDate = s.BirthDate,
            BirthCity = s.BirthCity,
            BirthCountry = string.IsNullOrWhiteSpace(s.BirthCountry) ? null : Resolve<BirthCountry>(BirthCountry.TryGetKnownValue, s.BirthCountry!, "birthCountry"),
            Nationalities = new[] { Resolve<Nationality>(Nationality.TryGetKnownValue, s.Nationality, "nationality") },
            Address = new Address
            {
                AddressLine1 = s.Address.Line1,
                Postcode = s.Address.Postcode,
                City = s.Address.City,
                Country = Resolve<Country>(Country.TryGetKnownValue, s.Address.Country, "address.country")
            }
            // Note: the shop collects phoneNumber at sign-up, but the Upvest BYOL user-create body has no field
            // for it (as with the identity document's expiryDate); both are captured by the shop and not sent.
        };

        var created = await ExecuteAsync("CreateUser", isWrite: true, c => _client.Users.CreateUser(
            new CreateUserRequest { IdempotencyKey = Key(buyerId, "create-user"), Body = userBody }, cancellationToken: c), ct);

        if (!created.TryGetUserByol(out var userByol))
            throw new UpvestApiException("Upvest returned an unexpected user shape on create.", HttpStatusCode.BadGateway);

        var userId = userByol.Id;
        _logger.LogInformation("Upvest user provisioned for investor {BuyerId}.", buyerId);

        Guid? accountGroupId = null;
        Guid? accountId = null;

        try
        {
            // 2) Record the identity document the shop checked (expiry is captured by the shop; Upvest has no field for it).
            var idType = ResolveIdentifierType(s.IdentityDocument.Type);
            await ExecuteAsync("CreateIdentifier", isWrite: true, c => _client.UserIdentifiers.CreateIdentifier(
                new CreateIdentifierRequest
                {
                    UserId = userId,
                    Body = new IdentifierCreateRequest
                    {
                        Type = idType,
                        IssuingCountry = Resolve<IssuingCountry>(IssuingCountry.TryGetKnownValue, s.IdentityDocument.IssuingCountry, "identityDocument.issuingCountry"),
                        Identifier = s.IdentityDocument.Number
                    }
                }, cancellationToken: c), ct);

            // 3) Record tax residency.
            await ExecuteAsync("SetTaxResidencies", isWrite: true, c => _client.TaxResidencies.SetTaxResidencies(
                new SetTaxResidenciesRequest
                {
                    UserId = userId,
                    IdempotencyKey = Key(buyerId, "set-tax"),
                    Body = new TaxResidenciesSetRequest
                    {
                        TaxResidencies = new TaxResidencyForCreateRequest[]
                        {
                            new WithTaxIdentifierNumber
                            {
                                Country = Resolve<Country>(Country.TryGetKnownValue, s.TaxCountry, "taxCountry"),
                                TaxIdentifierNumber = s.TaxId
                            }
                        }
                    }
                }, cancellationToken: c), ct);

            // 4) Open a PERSONAL account group.
            var groupResp = await ExecuteAsync("CreateAccountGroup", isWrite: true, c => _client.AccountGroups.CreateAccountGroup(
                new CreateAccountGroupRequest
                {
                    IdempotencyKey = Key(buyerId, "account-group"),
                    Body = new AccountGroupCreateUserRequest { UserId = userId, Type = Type13.Personal }
                }, cancellationToken: c), ct);
            if (groupResp.TryGetAccountGroup(out var group)) accountGroupId = group.Id;

            // 5) Open a PORTFOLIO account able to hold a managed portfolio.
            if (accountGroupId.HasValue)
            {
                var acctResp = await ExecuteAsync("CreateAccount", isWrite: true, c => _client.AccountsApi.CreateAccount(
                    new CreateAccountRequest
                    {
                        IdempotencyKey = Key(buyerId, "account"),
                        Body = new AccountCreateUserRequest
                        {
                            UserId = userId,
                            AccountGroupId = accountGroupId.Value,
                            Type = Type16.Portfolio
                        }
                    }, cancellationToken: c), ct);
                if (acctResp.TryGetAccount(out var account)) accountId = account.Id;
            }
        }
        catch (UpvestApiException ex) when (ex.IsScopeDenied)
        {
            // Expected in this sandbox: the client is authorised only for user operations. The user exists;
            // account provisioning could not be completed, so the investor remains pending (account not ready).
            _logger.LogInformation(
                "Investor {BuyerId}: account provisioning not permitted by current Upvest scopes; left pending.", buyerId);
        }

        var status = DeriveInvestorStatus(userByol.Status, accountGroupId, accountId, accountGroupActive: accountGroupId.HasValue && accountId.HasValue);
        return new InvestorProvisioningResult(userId, accountGroupId, accountId, status);
    }

    public async Task<InvestorStatus> GetInvestorStatusAsync(Guid userId, Guid? accountGroupId, Guid? accountId, CancellationToken ct)
    {
        var userResp = await ExecuteAsync("RetrieveUser", isWrite: false, c => _client.Users.RetrieveUser(
            new RetrieveUserRequest { UserId = userId }, cancellationToken: c), ct);
        var userStatus = userResp.TryGetUserByol(out var byol) ? byol.Status
                         : userResp.TryGetUserTol(out var tol) ? tol.Status
                         : Status.Active;

        if (userStatus == Status.Offboarded || userStatus == Status.Offboarding)
            return InvestorStatus.Rejected;

        if (!accountGroupId.HasValue || !accountId.HasValue)
            return InvestorStatus.Pending;

        try
        {
            var group = await ExecuteAsync("RetrieveAccountGroup", isWrite: false, c => _client.AccountGroups.RetrieveAccountGroup(
                new RetrieveAccountGroupRequest { AccountGroupId = accountGroupId.Value }, cancellationToken: c), ct);
            var account = await ExecuteAsync("RetrieveAccount", isWrite: false, c => _client.AccountsApi.RetrieveAccount(
                new RetrieveAccountRequest { AccountId = accountId.Value }, cancellationToken: c), ct);

            bool groupActive = group.TryGetAccountGroup(out var g) && g.Status == Status18.Active;
            bool accountActive = account.TryGetAccount(out var a) && a.Status == Status21.Active;
            return (userStatus == Status.Active && groupActive && accountActive)
                ? InvestorStatus.Active : InvestorStatus.Pending;
        }
        catch (UpvestApiException ex) when (ex.IsScopeDenied)
        {
            return InvestorStatus.Pending;
        }
    }

    // ---------------------------------------------------------------- Flow 2: models

    public async Task<Guid> SaveModelAllocationAsync(string modelId, Guid? existingAllocationId, string name, IReadOnlyList<ModelFundInput> funds, CancellationToken ct)
    {
        var allocation = funds.Select(f => new Allocation
        {
            InstrumentId = f.InstrumentId,                 // ISIN (implicit string → InstrumentId)
            InstrumentIdType = InstrumentIdType4.Isin,
            Weight = UpvestConversions.Weight(f.Weight)
        }).ToArray();

        if (existingAllocationId.HasValue)
        {
            var updated = await ExecuteAsync("UpdatePortfoliosAllocation", isWrite: true, c => _client.Portfolios.UpdatePortfoliosAllocation(
                new UpdatePortfoliosAllocationRequest
                {
                    AllocationId = existingAllocationId.Value,
                    Body = new PortfoliosAllocationUpdateRequest { Name = name, Allocation = allocation }
                }, cancellationToken: c), ct);
            return updated.Id;
        }

        var created = await ExecuteAsync("CreatePortfoliosAllocation", isWrite: true, c => _client.Portfolios.CreatePortfoliosAllocation(
            new CreatePortfoliosAllocationRequest
            {
                IdempotencyKey = Key("model", modelId),
                Body = new PortfoliosAllocationCreateRequest { Name = name, Allocation = allocation }
            }, cancellationToken: c), ct);
        return created.Id;
    }

    public async Task ConfigureFollowedModelAsync(string buyerId, Guid accountId, Guid allocationId, bool configurationExists, CancellationToken ct)
    {
        if (configurationExists)
        {
            await ExecuteAsync("UpdatePortfoliosConfiguration", isWrite: true, c => _client.Portfolios.UpdatePortfoliosConfiguration(
                new UpdatePortfoliosConfigurationRequest
                {
                    AccountId = accountId,
                    IdempotencyKey = Key(buyerId, "config", allocationId.ToString()),
                    Body = new PortfoliosConfigurationUpdateRequest { AllocationId = allocationId }
                }, cancellationToken: c), ct);
        }
        else
        {
            await ExecuteAsync("CreatePortfoliosConfiguration", isWrite: true, c => _client.Portfolios.CreatePortfoliosConfiguration(
                new CreatePortfoliosConfigurationRequest
                {
                    IdempotencyKey = Key(buyerId, "config", allocationId.ToString()),
                    Body = new PortfoliosConfigurationCreateRequest { AccountId = accountId, AllocationId = allocationId }
                }, cancellationToken: c), ct);
        }
    }

    public async Task<bool> HasConfigurationAsync(Guid accountId, CancellationToken ct)
    {
        try
        {
            await ExecuteAsync("RetrievePortfoliosConfiguration", isWrite: false, c => _client.Portfolios.RetrievePortfoliosConfiguration(
                new RetrievePortfoliosConfigurationRequest { AccountId = accountId }, cancellationToken: c), ct);
            return true;
        }
        catch (UpvestApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    // ---------------------------------------------------------------- Flow 3: pay in

    public async Task<PaymentPlacement> PayInAsync(string paymentId, Guid userId, Guid accountGroupId, Guid accountId, decimal amount, CancellationToken ct)
    {
        var amountStr = UpvestConversions.Amount(amount);

        // Sandbox cash-in: credit the account group so the portfolio order has cash to invest.
        var topup = await ExecuteAsync("CreateTopup", isWrite: true, c => _client.TopUps.CreateTopup(
            new CreateTopupRequest
            {
                IdempotencyKey = Key(paymentId, "topup"),
                Body = new PaymentsTopUpCreateRequest { AccountGroupId = accountGroupId, CashAmount = amountStr, Currency = Currency.Eur }
            }, cancellationToken: c), ct);

        // Invest the cash across the followed model's weights via a BUY portfolio order.
        PortfoliosOrder order;
        try
        {
            order = await ExecuteAsync("CreatePortfoliosOrder", isWrite: true, c => _client.Portfolios.CreatePortfoliosOrder(
                new CreatePortfoliosOrderRequest
                {
                    IdempotencyKey = Key(paymentId, "order"),
                    Body = new PortfoliosOrderPlaceRequest
                    {
                        UserId = userId,
                        AccountId = accountId,
                        CashAmount = amountStr,
                        Currency = Currency.Eur,
                        Side = Side15.Buy
                    }
                }, cancellationToken: c), ct);
        }
        catch (UpvestApiException ex) when (ex.OutcomeUnknown)
        {
            // The order may have reached Upvest before the connection failed: re-read by account and settle.
            var reconciled = await TryFindOrderByAmountAsync(accountId, amountStr, ct);
            if (reconciled is not null)
                return new PaymentPlacement(topup.Id, reconciled.Id, MapOrderStatus(reconciled.Status));
            throw; // still unknown — surfaced to the caller as outcome-unknown
        }

        return new PaymentPlacement(topup.Id, order.Id, MapOrderStatus(order.Status));
    }

    public async Task<PaymentStatus> GetPaymentStatusAsync(Guid orderId, CancellationToken ct)
    {
        var order = await ExecuteAsync("RetrievePortfoliosOrder", isWrite: false, c => _client.Portfolios.RetrievePortfoliosOrder(
            new RetrievePortfoliosOrderRequest { PortfolioOrderId = orderId }, cancellationToken: c), ct);
        return MapOrderStatus(order.Status);
    }

    private async Task<PortfoliosOrder?> TryFindOrderByAmountAsync(Guid accountId, string amountStr, CancellationToken ct)
    {
        try
        {
            var list = await ExecuteAsync("ListPortfoliosOrders", isWrite: false, c => _client.Portfolios.ListPortfoliosOrders(
                new ListPortfoliosOrdersRequest { AccountId = accountId }, cancellationToken: c), ct);
            return list.Data
                .Where(o => o.CashAmount == amountStr)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefault();
        }
        catch (UpvestApiException)
        {
            return null; // reconciliation itself failed — outcome stays unknown
        }
    }

    // ---------------------------------------------------------------- Flow 4: realignment

    public async Task<Guid> TriggerRealignmentAsync(string realignmentId, Guid allocationId, CancellationToken ct)
    {
        var resp = await ExecuteAsync("TriggerPortfolioRebalancing", isWrite: true, c => _client.PortfoliosRebalancing.TriggerPortfolioRebalancing(
            new TriggerPortfolioRebalancingOperationRequest
            {
                IdempotencyKey = Key("realignment", realignmentId),
                Body = new Allocations { AllocationsValue = new[] { allocationId } }
            }, cancellationToken: c), ct);
        return resp.Id;
    }

    public async Task<RealignmentStatus> GetRealignmentStatusAsync(Guid executionId, CancellationToken ct)
    {
        // The execution resource carries no status; derive it from its execution orders.
        var orders = await ExecuteAsync("ListPortfolioRebalancingExecutionOrders", isWrite: false, c =>
            _client.PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders(
                new ListPortfolioRebalancingExecutionOrdersRequest { ExecutionId = executionId }, cancellationToken: c), ct);

        var statuses = orders.Data.Select(o => o.Status).ToList();
        if (statuses.Count == 0)
            return RealignmentStatus.Pending; // triggered, no orders reported yet
        if (statuses.Any(st => st == Status71.New || st == Status71.Processing))
            return RealignmentStatus.Pending;
        if (statuses.Any(st => st == Status71.Cancelled))
            return RealignmentStatus.Failed;
        return RealignmentStatus.Completed; // all Filled
    }

    // ---------------------------------------------------------------- Flow 5: portfolio view

    public async Task<PortfolioSnapshot> GetPortfolioAsync(Guid accountId, CancellationToken ct)
    {
        Guid? allocationId = null;
        try
        {
            var config = await ExecuteAsync("RetrievePortfoliosConfiguration", isWrite: false, c => _client.Portfolios.RetrievePortfoliosConfiguration(
                new RetrievePortfoliosConfigurationRequest { AccountId = accountId }, cancellationToken: c), ct);
            allocationId = config.AllocationId;
        }
        catch (UpvestApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            allocationId = null; // no model followed yet
        }

        var valuation = await ExecuteAsync("GetAccountValuation", isWrite: false, c => _client.Valuations.GetAccountValuation(
            new GetAccountValuationRequest { AccountId = accountId, PriceQuality = PriceQuality3.HighestAvailable }, cancellationToken: c), ct);

        var holdings = valuation.SecurityPositions.Select(p => new Holding(
            InstrumentId: p.Instrument.Isin ?? p.Instrument.Uuid.ToString(),
            Quantity: UpvestConversions.ParseDecimal(p.Quantity),
            Value: p.Value is null ? 0m : UpvestConversions.ParseDecimal(p.Value.Amount))).ToList();

        var totalValue = UpvestConversions.ParseDecimal(valuation.TotalSecurityValue.Amount);
        return new PortfolioSnapshot(allocationId, holdings, totalValue);
    }

    // ---------------------------------------------------------------- helpers

    private static Guid Key(params string[] parts) => UpvestConversions.DeterministicKey(parts);

    private static Type7 ResolveIdentifierType(string type)
    {
        var normalized = type.Trim().ToUpperInvariant().Replace(" ", "_");
        return normalized switch
        {
            "PASSPORT" => Type7.Passport,
            "NATIONAL_ID" or "NATIONALID" => Type7.NationalId,
            _ => throw new UpvestApiException($"Unsupported identity document type '{type}'.", HttpStatusCode.BadRequest)
        };
    }

    /// <summary>Signature of the generated <c>TryGetKnownValue</c> on each open string enum.</summary>
    private delegate bool TryGetEnum<T>(string? value, out T? result);

    /// <summary>
    /// Resolve a caller-supplied code (e.g. an ISO country code) to a known Upvest enum constant via the enum's
    /// own <c>TryGetKnownValue</c>; reject an undeclared code as a 400 so a bad code never reaches Upvest.
    /// </summary>
    private static T Resolve<T>(TryGetEnum<T> tryGet, string value, string field) where T : class
    {
        if (tryGet(value.Trim().ToUpperInvariant(), out var known) && known is not null)
            return known;
        throw new UpvestApiException($"Unknown value for {field}.", HttpStatusCode.BadRequest);
    }

    private static InvestorStatus DeriveInvestorStatus(Status userStatus, Guid? accountGroupId, Guid? accountId, bool accountGroupActive)
    {
        if (userStatus == Status.Offboarded || userStatus == Status.Offboarding)
            return InvestorStatus.Rejected;
        if (userStatus == Status.Active && accountGroupId.HasValue && accountId.HasValue)
            return InvestorStatus.Active;
        return InvestorStatus.Pending;
    }

    private static PaymentStatus MapOrderStatus(Status65 status)
    {
        if (status == Status65.Filled || status == Status65.Settled) return PaymentStatus.Invested;
        if (status == Status65.Cancelled) return PaymentStatus.Failed;
        return PaymentStatus.Pending; // New, Processing
    }

    /// <summary>
    /// Wrap every SDK call: translate the SDK exception family into a single <see cref="UpvestApiException"/>,
    /// logging only the operation name and status (never a request body or personal data).
    /// </summary>
    private async Task<T> ExecuteAsync<T>(string operation, bool isWrite, Func<CancellationToken, Task<T>> call, CancellationToken ct)
    {
        try
        {
            return await call(ct);
        }
        catch (ResponseDeserializationException ex)
        {
            // The provider answered but the body did not match the declared type. Keep the status so a 4xx is
            // not retried forever; a 2xx here is a genuinely unknown outcome.
            bool unknown = isWrite && IsSuccess(ex.StatusCode);
            _logger.LogWarning("Upvest {Operation} returned an unprocessable response ({Status}).", operation, (int)ex.StatusCode);
            throw new UpvestApiException("Upvest returned a response that could not be processed.", ex.StatusCode, unknown, ex);
        }
        catch (ApiException ex)
        {
            if (ex.StatusCode == HttpStatusCode.Unauthorized)
                _logger.LogInformation("Upvest {Operation} not permitted by current scopes (401).", operation);
            else
                _logger.LogWarning("Upvest {Operation} failed with status {Status}.", operation, (int)ex.StatusCode);
            throw new UpvestApiException($"Upvest {operation} was rejected.", ex.StatusCode, outcomeUnknown: false, ex);
        }
        catch (SdkTimeoutException ex)
        {
            _logger.LogWarning("Upvest {Operation} timed out.", operation);
            throw new UpvestApiException($"Upvest {operation} did not answer in time.", null, outcomeUnknown: isWrite, ex);
        }
        catch (SdkConnectionException ex)
        {
            _logger.LogWarning("Upvest {Operation} could not reach the provider.", operation);
            throw new UpvestApiException($"Upvest {operation} could not reach the provider.", null, outcomeUnknown: isWrite, ex);
        }
        catch (AuthSchemeException ex)
        {
            _logger.LogError(ex, "Upvest {Operation} could not be authenticated (credential configuration).", operation);
            throw new UpvestApiException("Upvest credentials are missing or could not be applied.", null, outcomeUnknown: false, ex);
        }
    }

    private static bool IsSuccess(HttpStatusCode code) => (int)code is >= 200 and < 300;
}
