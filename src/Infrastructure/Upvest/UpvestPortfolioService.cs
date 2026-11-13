using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
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
using UpvestInvestmentApi.Requests.UserIdentifiers;
using UpvestInvestmentApi.Requests.Users;
using UpvestInvestmentApi.Requests.Valuations;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The managed-investment-portfolio capability, implemented against the Upvest Investment API SDK
/// and the application's own data layer. Each per-shopper method keys on the shopper's user name so
/// one shopper can never see another's investor record, payments or portfolio. Every SDK failure is
/// translated into a <see cref="UpvestException"/> at this boundary; no SDK type or personal detail
/// escapes it, and request bodies carrying personal data are never logged.
/// </summary>
public class UpvestPortfolioService : IInvestorPortfolioService
{
    // The whole-call budget a caller actually experiences, enforced with a linked CancellationToken.
    private static readonly TimeSpan CallBudget = TimeSpan.FromSeconds(45);

    private const string Eur = "EUR";
    private const int PageSize = 100;
    private const int MaxPages = 50; // backstop so a paged read can never spin unbounded

    private readonly UpvestInvestmentApiClient _client;
    private readonly IRepository<InvestorRecord> _investors;
    private readonly IRepository<ModelPortfolio> _models;
    private readonly IRepository<PortfolioPayment> _payments;
    private readonly IRepository<Realignment> _realignments;
    private readonly ILogger<UpvestPortfolioService> _logger;

    public UpvestPortfolioService(
        UpvestInvestmentApiClient client,
        IRepository<InvestorRecord> investors,
        IRepository<ModelPortfolio> models,
        IRepository<PortfolioPayment> payments,
        IRepository<Realignment> realignments,
        ILogger<UpvestPortfolioService> logger)
    {
        _client = client;
        _investors = investors;
        _models = models;
        _payments = payments;
        _realignments = realignments;
        _logger = logger;
    }

    // ─── Flow 1 — becoming an investor ──────────────────────────────────────────────────────────

    public async Task<InvestorStatusResult> SignUpInvestorAsync(
        string shopperUserName, InvestorSignupInput signup, CancellationToken cancellationToken = default)
    {
        // One investor per shopper. If they already signed up, return their current status (the POST
        // is idempotent per shopper) rather than creating a second user at Upvest.
        var existing = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperUserName), cancellationToken);
        if (existing is not null)
        {
            return await RefreshInvestorAsync(existing, cancellationToken);
        }

        // Claim the record first, so a concurrent second sign-up is refused by the unique key.
        var record = new InvestorRecord(shopperUserName);
        try
        {
            record = await _investors.AddAsync(record, cancellationToken);
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            var reloaded = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperUserName), cancellationToken);
            if (reloaded is not null) return await RefreshInvestorAsync(reloaded, cancellationToken);
            throw;
        }

        // Create the user at Upvest (users:admin — authorised in this sandbox). This is the essential
        // step; without it there is no investor, so a failure here removes the claim and surfaces.
        UserCreateRequest1 created;
        try
        {
            created = await CallAsync("Users.CreateUser", ct => _client.Users.CreateUser(new CreateUserRequest
            {
                IdempotencyKey = record.UserIdempotencyKey,
                Body = UserCreateRequest.UserTolCreateRequest(BuildUserBody(signup))
            }, cancellationToken: ct), cancellationToken);
        }
        catch (UpvestException ex) when (!ex.UnknownOutcome)
        {
            await _investors.DeleteAsync(record, cancellationToken);
            throw;
        }

        var newUserId = ReadCreatedUserId(created);
        if (newUserId is Guid createdId)
        {
            record.SetUpvestUser(createdId);
            _logger.LogInformation("Upvest user created for investor sign-up {Shopper}: {UserId}", shopperUserName, createdId);
        }
        else
        {
            _logger.LogWarning("Upvest CreateUser returned a body with no recognisable user id for {Shopper}.", shopperUserName);
        }
        await _investors.UpdateAsync(record, cancellationToken);

        // Best-effort remainder of the onboarding chain. In this sandbox these scopes return 401; that
        // is a known, surfaced outcome, so each step is logged and the record is left pending rather
        // than failing the whole sign-up.
        var userId = record.UpvestUserId;
        if (userId is Guid uid)
        {
            await TryStepAsync("TaxResidencies.SetTaxResidencies", async () =>
            {
                await CallAsync("TaxResidencies.SetTaxResidencies", ct => _client.TaxResidencies.SetTaxResidencies(new SetTaxResidenciesRequest
                {
                    UserId = uid,
                    IdempotencyKey = record.TaxIdempotencyKey,
                    Body = new TaxResidenciesSetRequest
                    {
                        TaxResidencies = new[]
                        {
                            TaxResidencyForCreateRequest.WithTaxIdentifierNumber(new WithTaxIdentifierNumber
                            {
                                Country = ResolveCountry(signup.TaxCountry),
                                TaxIdentifierNumber = signup.TaxId
                            })
                        }
                    }
                }, cancellationToken: ct), cancellationToken);
                record.MarkTaxResidencySet();
            });

            await TryStepAsync("UserIdentifiers.CreateIdentifier", async () =>
            {
                await CallAsync("UserIdentifiers.CreateIdentifier", ct => _client.UserIdentifiers.CreateIdentifier(new CreateIdentifierRequest
                {
                    UserId = uid,
                    Body = new IdentifierCreateRequest
                    {
                        Type = ResolveDocumentType(signup.IdentityDocument.Type),
                        IssuingCountry = ResolveIssuingCountry(signup.IdentityDocument.IssuingCountry),
                        Identifier = signup.IdentityDocument.Number
                    }
                }, cancellationToken: ct), cancellationToken);
                record.MarkIdentifierSet();
            });

            Guid? accountGroupId = null;
            await TryStepAsync("AccountGroups.CreateAccountGroup", async () =>
            {
                var group = await CallAsync("AccountGroups.CreateAccountGroup", ct => _client.AccountGroups.CreateAccountGroup(new CreateAccountGroupRequest
                {
                    IdempotencyKey = record.AccountGroupIdempotencyKey,
                    Body = AccountGroupCreateRequest.AccountGroupCreateUserRequest(new AccountGroupCreateUserRequest
                    {
                        UserId = uid,
                        Type = Type13.Personal
                    })
                }, cancellationToken: ct), cancellationToken);
                if (group.TryGetAccountGroup(out var ag)) accountGroupId = ag.Id;
            });

            if (accountGroupId is Guid groupId)
            {
                await TryStepAsync("AccountsApi.CreateAccount", async () =>
                {
                    var account = await CallAsync("AccountsApi.CreateAccount", ct => _client.AccountsApi.CreateAccount(new CreateAccountRequest
                    {
                        IdempotencyKey = record.AccountIdempotencyKey,
                        Body = AccountCreateRequest.AccountCreateUserRequest(new AccountCreateUserRequest
                        {
                            UserId = uid,
                            AccountGroupId = groupId,
                            Type = Type16.Portfolio,
                            Name = "Managed portfolio"
                        })
                    }, cancellationToken: ct), cancellationToken);
                    if (account.TryGetAccount(out var acc)) record.SetAccount(groupId, acc.Id);
                });
            }
        }

        var status = await DeriveInvestorStatusAsync(record, cancellationToken);
        record.SetStatus(status);
        await _investors.UpdateAsync(record, cancellationToken);
        return new InvestorStatusResult(record.UpvestUserId, record.Status);
    }

    public async Task<InvestorStatusResult?> GetInvestorAsync(string shopperUserName, CancellationToken cancellationToken = default)
    {
        var record = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperUserName), cancellationToken);
        if (record is null) return null;
        return await RefreshInvestorAsync(record, cancellationToken);
    }

    private async Task<InvestorStatusResult> RefreshInvestorAsync(InvestorRecord record, CancellationToken cancellationToken)
    {
        var status = await DeriveInvestorStatusAsync(record, cancellationToken);
        if (status != record.Status)
        {
            record.SetStatus(status);
            await _investors.UpdateAsync(record, cancellationToken);
        }
        return new InvestorStatusResult(record.UpvestUserId, record.Status);
    }

    /// <summary>
    /// Derives the caller-facing investor status from the user's and account group's state at Upvest.
    /// Reflects what Upvest records, never a locally-assumed value.
    /// </summary>
    private async Task<string> DeriveInvestorStatusAsync(InvestorRecord record, CancellationToken cancellationToken)
    {
        if (record.UpvestUserId is not Guid userId) return PortfolioStatuses.Investor.Pending;

        string? userStatus = null;
        try
        {
            var user = await CallAsync("Users.RetrieveUser", ct => _client.Users.RetrieveUser(
                new RetrieveUserRequest { UserId = userId }, cancellationToken: ct), cancellationToken);
            userStatus = ReadUserStatus(user);
        }
        catch (UpvestException ex)
        {
            _logger.LogWarning("Could not refresh investor status at Upvest for user {UserId}: {Message}", userId, ex.Message);
            return record.Status; // keep last known
        }

        if (userStatus is "OFFBOARDING" or "OFFBOARDED") return PortfolioStatuses.Investor.Rejected;

        // Active requires the user to have passed onboarding AND an account able to hold a portfolio.
        bool accountReady = false;
        if (record.AccountGroupId is Guid groupId)
        {
            try
            {
                var group = await CallAsync("AccountGroups.RetrieveAccountGroup", ct => _client.AccountGroups.RetrieveAccountGroup(
                    new RetrieveAccountGroupRequest { AccountGroupId = groupId }, cancellationToken: ct), cancellationToken);
                if (group.TryGetAccountGroup(out var ag)) accountReady = ag.Status.Is("ACTIVE");
            }
            catch (UpvestException ex)
            {
                _logger.LogWarning("Could not refresh account-group readiness for {GroupId}: {Message}", groupId, ex.Message);
            }
        }

        return (userStatus == "ACTIVE" && accountReady && record.AccountId is not null)
            ? PortfolioStatuses.Investor.Active
            : PortfolioStatuses.Investor.Pending;
    }

    // The create/get user responses are AnyOf(UserByol, UserTol); the sandbox may answer with either
    // arm, so read the id/status from whichever matched (with a raw-JSON fallback).
    private static Guid? ReadCreatedUserId(UserCreateRequest1 r) => r.Match(
        onUserByol: u => (Guid?)u.Id,
        onUserTol: t => (Guid?)t.Id,
        otherwise: raw => raw.ValueKind == JsonValueKind.Object && raw.TryGetProperty("id", out var p) && p.TryGetGuid(out var g) ? g : null);

    private static string? ReadUserStatus(UserGetResponse r) => r.Match(
        onUserByol: u => u.Status.Value,
        onUserTol: t => t.Status.Value,
        otherwise: raw => raw.ValueKind == JsonValueKind.Object && raw.TryGetProperty("status", out var p) ? p.GetString() : null);

    private static UserTolCreateRequest BuildUserBody(InvestorSignupInput s) => new()
    {
        FirstName = s.FirstName,
        LastName = s.LastName,
        Email = s.Email,
        BirthDate = s.BirthDate,
        BirthCity = s.BirthCity,
        BirthCountry = string.IsNullOrWhiteSpace(s.BirthCountry) ? null : ResolveBirthCountry(s.BirthCountry!),
        Nationalities = new[] { ResolveNationality(s.Nationality) },
        PhoneNumber = string.IsNullOrWhiteSpace(s.PhoneNumber) ? null : s.PhoneNumber,
        Address = new Address2
        {
            AddressLine1 = s.Address.Line1,
            Postcode = s.Address.Postcode,
            City = s.Address.City,
            Country = ResolveCountry(s.Address.Country)
        },
        // FATCA consent is required by Upvest but not part of the shop's form; a non-US shopper with
        // the confirmation timestamp set at sign-up is the documented default.
        Fatca = new Fatca { Status = false, ConfirmedAt = DateTimeOffset.UtcNow }
    };

    // ─── Flow 2 — model portfolios ──────────────────────────────────────────────────────────────

    public async Task<ModelPortfolioResult> UpsertModelAsync(
        string modelId, ModelPortfolioInput model, CancellationToken cancellationToken = default)
    {
        if (model.Funds is null || model.Funds.Count == 0)
            throw new PortfolioValidationException("A model portfolio must have at least one fund.");

        var totalWeight = model.Funds.Sum(f => f.Weight);
        if (totalWeight != 100m)
            throw new PortfolioValidationException($"Model portfolio fund weights must sum to exactly 100, but summed to {totalWeight}.");

        var funds = model.Funds.Select(f => new ModelFund(f.InstrumentId, f.Weight)).ToList();
        var allocation = model.Funds
            .Select(f => new Allocation { InstrumentId = f.InstrumentId, Weight = FormatWeight(f.Weight) })
            .ToList();

        // The model definition is owned by this application (operator-defined). Persist it first; the
        // Upvest allocation is a sync of that definition.
        var existing = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), cancellationToken);
        bool isNew = existing is null;
        ModelPortfolio record;

        if (isNew)
        {
            record = new ModelPortfolio(modelId, model.Name, funds);
            try
            {
                record = await _models.AddAsync(record, cancellationToken);
            }
            catch (Exception ex) when (IsUniqueViolation(ex))
            {
                var reloaded = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), cancellationToken);
                if (reloaded is null) throw;
                record = reloaded;
                isNew = false;
                record.Replace(model.Name, funds);
            }
        }
        else
        {
            record = existing!;
            record.Replace(model.Name, funds);
        }

        // Sync to Upvest. A caller fault (e.g. an invalid ISIN the provider rejects) is surfaced; a
        // provider/our-side failure (auth, scope, transport) leaves the definition saved and the sync
        // deferred to the next upsert or realignment.
        try
        {
            if (record.UpvestAllocationId is Guid allocationId)
            {
                await CallAsync("Portfolios.UpdatePortfoliosAllocation", ct => _client.Portfolios.UpdatePortfoliosAllocation(new UpdatePortfoliosAllocationRequest
                {
                    AllocationId = allocationId,
                    Body = new PortfoliosAllocationUpdateRequest { Name = model.Name, Allocation = allocation }
                }, cancellationToken: ct), cancellationToken);
            }
            else
            {
                var allocationResult = await CallAsync("Portfolios.CreatePortfoliosAllocation", ct => _client.Portfolios.CreatePortfoliosAllocation(new CreatePortfoliosAllocationRequest
                {
                    IdempotencyKey = record.AllocationIdempotencyKey,
                    Body = new PortfoliosAllocationCreateRequest { Name = model.Name, Allocation = allocation }
                }, cancellationToken: ct), cancellationToken);
                record.SetUpvestAllocation(allocationResult.Id);
            }
        }
        catch (UpvestException ex) when (ex.CallerFault)
        {
            if (isNew) await _models.DeleteAsync(record, cancellationToken);
            throw;
        }
        catch (UpvestException ex)
        {
            _logger.LogWarning("Model '{ModelId}' saved; Upvest allocation sync deferred: {Message}", modelId, ex.Message);
        }

        await _models.UpdateAsync(record, cancellationToken);
        return ToModelResult(record);
    }

    public async Task<IReadOnlyList<ModelPortfolioResult>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        // Models are defined and owned by this application; its own store is the source of truth for
        // their funds and weights (Upvest has no notion of a model's name or app id).
        var models = await _models.ListAsync(cancellationToken);
        return models.Select(ToModelResult).ToList();
    }

    public async Task SelectModelAsync(string shopperUserName, string modelId, CancellationToken cancellationToken = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperUserName), cancellationToken)
            ?? throw new InvestorStateException("You are not registered as an investor. Sign up first.");

        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), cancellationToken)
            ?? throw new PortfolioNotFoundException($"No model portfolio '{modelId}' exists.");

        if (investor.AccountId is not Guid accountId)
            throw new InvestorStateException("Your investor account is not ready yet; try again once your sign-up is active.");

        if (model.UpvestAllocationId is not Guid allocationId)
            throw new PortfolioValidationException($"Model '{modelId}' has not been synced to Upvest yet.");

        // One active configuration per account: create the first time, update to switch models later.
        PortfoliosConfiguration? current = null;
        try
        {
            current = await CallAsync("Portfolios.RetrievePortfoliosConfiguration", ct => _client.Portfolios.RetrievePortfoliosConfiguration(
                new RetrievePortfoliosConfigurationRequest { AccountId = accountId }, cancellationToken: ct), cancellationToken);
        }
        catch (UpvestException ex) when (ex.ProviderStatusCode == 404)
        {
            current = null; // no configuration yet
        }

        if (current is null)
        {
            await CallAsync("Portfolios.CreatePortfoliosConfiguration", ct => _client.Portfolios.CreatePortfoliosConfiguration(new CreatePortfoliosConfigurationRequest
            {
                IdempotencyKey = investor.ConfigurationIdempotencyKey,
                Body = new PortfoliosConfigurationCreateRequest { AccountId = accountId, AllocationId = allocationId }
            }, cancellationToken: ct), cancellationToken);
        }
        else
        {
            await CallAsync("Portfolios.UpdatePortfoliosConfiguration", ct => _client.Portfolios.UpdatePortfoliosConfiguration(new UpdatePortfoliosConfigurationRequest
            {
                AccountId = accountId,
                IdempotencyKey = investor.ConfigurationIdempotencyKey,
                Body = new PortfoliosConfigurationUpdateRequest { AllocationId = allocationId }
            }, cancellationToken: ct), cancellationToken);
        }

        investor.SelectModel(modelId);
        await _investors.UpdateAsync(investor, cancellationToken);
    }

    // ─── Flow 3 — paying in ─────────────────────────────────────────────────────────────────────

    public async Task<PaymentResult> PayInAsync(string shopperUserName, decimal amount, CancellationToken cancellationToken = default)
    {
        if (amount <= 0m) throw new PortfolioValidationException("The pay-in amount must be greater than zero.");

        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperUserName), cancellationToken)
            ?? throw new InvestorStateException("You are not registered as an investor. Sign up first.");

        if (!investor.IsActive || investor.AccountId is not Guid accountId || investor.UpvestUserId is not Guid userId)
            throw new InvestorStateException("You must be an accepted investor with a ready account before you can pay in.");

        if (string.IsNullOrEmpty(investor.SelectedModelId))
            throw new InvestorStateException("Choose a model portfolio before paying in.");

        // Claim the payment first, so a second submission is a distinct claim and the order carries a
        // stable idempotency key for reconciliation.
        var payment = await _payments.AddAsync(new PortfolioPayment(shopperUserName, amount, Eur), cancellationToken);

        try
        {
            var order = await CallAsync("Portfolios.CreatePortfoliosOrder", ct => _client.Portfolios.CreatePortfoliosOrder(new CreatePortfoliosOrderRequest
            {
                IdempotencyKey = payment.OrderIdempotencyKey,
                Body = new PortfoliosOrderPlaceRequest
                {
                    UserId = userId,
                    AccountId = accountId,
                    CashAmount = FormatMoney(amount),
                    Currency = ResolveCurrency(Eur),
                    Side = ResolveBuySide()
                }
            }, cancellationToken: ct), cancellationToken);

            payment.SetUpvestOrder(order.Id);
            payment.SetStatus(MapOrderStatus(order.Status.Value));
        }
        catch (UpvestException ex) when (ex.UnknownOutcome)
        {
            // The order may have reached Upvest; leave the payment pending and let GET /payments settle
            // it from the order's status later (reconciliation by the stored idempotency key / order id).
            _logger.LogWarning("Pay-in order outcome unknown for payment {PaymentId}; left pending: {Message}", payment.PaymentId, ex.Message);
        }
        catch (UpvestException)
        {
            payment.SetStatus(PortfolioStatuses.Payment.Failed);
            await _payments.UpdateAsync(payment, cancellationToken);
            throw;
        }

        await _payments.UpdateAsync(payment, cancellationToken);
        return new PaymentResult(payment.PaymentId, payment.Amount, payment.Status);
    }

    public async Task<IReadOnlyList<PaymentResult>> ListPaymentsAsync(string shopperUserName, CancellationToken cancellationToken = default)
    {
        // Settle any pending payment whose order outcome is now known at Upvest.
        var pending = await _payments.ListAsync(new PendingPaymentsByShopperSpecification(shopperUserName), cancellationToken);
        foreach (var p in pending)
        {
            if (p.UpvestOrderId is not Guid orderId) continue;
            try
            {
                var order = await CallAsync("Portfolios.RetrievePortfoliosOrder", ct => _client.Portfolios.RetrievePortfoliosOrder(
                    new RetrievePortfoliosOrderRequest { PortfolioOrderId = orderId }, cancellationToken: ct), cancellationToken);
                var mapped = MapOrderStatus(order.Status.Value);
                if (mapped != p.Status)
                {
                    p.SetStatus(mapped);
                    await _payments.UpdateAsync(p, cancellationToken);
                }
            }
            catch (UpvestException ex)
            {
                _logger.LogWarning("Could not refresh payment {PaymentId} from Upvest: {Message}", p.PaymentId, ex.Message);
            }
        }

        var all = await _payments.ListAsync(new PaymentsByShopperSpecification(shopperUserName), cancellationToken);
        return all.Select(p => new PaymentResult(p.PaymentId, p.Amount, p.Status)).ToList();
    }

    // ─── Flow 4 — realignment ───────────────────────────────────────────────────────────────────

    public async Task<RealignmentResult> TriggerRealignmentAsync(string modelId, CancellationToken cancellationToken = default)
    {
        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), cancellationToken)
            ?? throw new PortfolioNotFoundException($"No model portfolio '{modelId}' exists.");

        if (model.UpvestAllocationId is not Guid allocationId)
            throw new PortfolioValidationException($"Model '{modelId}' has not been synced to Upvest yet.");

        var realignment = await _realignments.AddAsync(new Realignment(modelId), cancellationToken);

        try
        {
            var response = await CallAsync("PortfoliosRebalancing.TriggerPortfolioRebalancing", ct => _client.PortfoliosRebalancing.TriggerPortfolioRebalancing(new TriggerPortfolioRebalancingOperationRequest
            {
                IdempotencyKey = realignment.IdempotencyKey,
                Body = TriggerPortfolioRebalancingRequest.Allocations(new Allocations { AllocationsValue = new[] { allocationId } })
            }, cancellationToken: ct), cancellationToken);

            realignment.SetUpvestExecution(response.Id);
        }
        catch (UpvestException ex) when (ex.UnknownOutcome)
        {
            _logger.LogWarning("Realignment trigger outcome unknown for {RealignmentId}; left pending: {Message}", realignment.RealignmentId, ex.Message);
        }
        catch (UpvestException)
        {
            realignment.SetStatus(PortfolioStatuses.Realignment.Failed);
            await _realignments.UpdateAsync(realignment, cancellationToken);
            throw;
        }

        await _realignments.UpdateAsync(realignment, cancellationToken);
        return new RealignmentResult(realignment.RealignmentId, realignment.Status);
    }

    public async Task<RealignmentResult?> GetRealignmentAsync(Guid realignmentId, CancellationToken cancellationToken = default)
    {
        var realignment = await _realignments.FirstOrDefaultAsync(new RealignmentByIdSpecification(realignmentId), cancellationToken);
        if (realignment is null) return null;

        if (realignment.UpvestExecutionId is Guid executionId && realignment.Status == PortfolioStatuses.Realignment.Pending)
        {
            try
            {
                var (orders, truncated) = await PageAsync<PortfoliosRebalancingExecutionOrder>(
                    (offset, limit, ct) => FetchExecutionOrders(executionId, offset, limit, ct),
                    cancellationToken);

                var derived = DeriveRealignmentStatus(orders, truncated);
                if (derived != realignment.Status)
                {
                    realignment.SetStatus(derived);
                    await _realignments.UpdateAsync(realignment, cancellationToken);
                }
            }
            catch (UpvestException ex)
            {
                _logger.LogWarning("Could not refresh realignment {RealignmentId} from Upvest: {Message}", realignmentId, ex.Message);
            }
        }

        return new RealignmentResult(realignment.RealignmentId, realignment.Status);
    }

    private async Task<(IReadOnlyList<PortfoliosRebalancingExecutionOrder> Data, Meta Meta)> FetchExecutionOrders(
        Guid executionId, int offset, int limit, CancellationToken ct)
    {
        var response = await CallAsync("PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders",
            c => _client.PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders(
                new ListPortfolioRebalancingExecutionOrdersRequest { ExecutionId = executionId, Offset = offset, Limit = limit },
                cancellationToken: c), ct);
        return (response.Data, response.Meta);
    }

    private static string DeriveRealignmentStatus(IReadOnlyList<PortfoliosRebalancingExecutionOrder> orders, bool truncated)
    {
        // No status field on the execution itself; derive it from the per-account execution orders.
        if (truncated || orders.Count == 0) return PortfolioStatuses.Realignment.Pending;

        var statuses = orders.Select(o => o.Status.Value).ToList();
        if (statuses.Any(s => s is "NEW" or "PROCESSING")) return PortfolioStatuses.Realignment.Pending;
        if (statuses.All(s => s == "FILLED")) return PortfolioStatuses.Realignment.Completed;
        return PortfolioStatuses.Realignment.Failed; // at least one CANCELLED and none pending
    }

    // ─── Flow 5 — the investor's portfolio ──────────────────────────────────────────────────────

    public async Task<PortfolioResult> GetPortfolioAsync(string shopperUserName, CancellationToken cancellationToken = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperUserName), cancellationToken)
            ?? throw new InvestorStateException("You are not registered as an investor. Sign up first.");

        if (!investor.IsActive || investor.AccountId is not Guid accountId)
            throw new InvestorStateException("Your investor account is not ready yet.");

        // The model the account currently follows, as Upvest records it (mapped back to the app model id).
        string? modelId = null;
        try
        {
            var config = await CallAsync("Portfolios.RetrievePortfoliosConfiguration", ct => _client.Portfolios.RetrievePortfoliosConfiguration(
                new RetrievePortfoliosConfigurationRequest { AccountId = accountId }, cancellationToken: ct), cancellationToken);
            modelId = await MapAllocationToModelId(config.AllocationId, cancellationToken);
        }
        catch (UpvestException ex) when (ex.ProviderStatusCode == 404)
        {
            modelId = investor.SelectedModelId;
        }

        // Holdings and total value, as Upvest currently values the account.
        var valuation = await CallAsync("Valuations.GetAccountValuation", ct => _client.Valuations.GetAccountValuation(
            new GetAccountValuationRequest { AccountId = accountId, PriceQuality = PriceQuality3.Eod }, cancellationToken: ct), cancellationToken);

        var holdings = valuation.SecurityPositions.Select(sp => new HoldingResult(
            sp.Instrument.Isin ?? sp.Instrument.Uuid.ToString(),
            ParseDecimal(sp.Quantity),
            sp.Value is null ? 0m : ParseDecimal(sp.Value.Amount))).ToList();

        var totalValue = ParseDecimal(valuation.TotalSecurityValue.Amount);

        var payments = await _payments.ListAsync(new PaymentsByShopperSpecification(shopperUserName), cancellationToken);
        var paidIn = payments.Where(p => p.Status != PortfolioStatuses.Payment.Failed).Sum(p => p.Amount);

        return new PortfolioResult(modelId, holdings, totalValue, paidIn);
    }

    private async Task<string?> MapAllocationToModelId(Guid allocationId, CancellationToken cancellationToken)
    {
        var models = await _models.ListAsync(cancellationToken);
        return models.FirstOrDefault(m => m.UpvestAllocationId == allocationId)?.ModelId ?? allocationId.ToString();
    }

    // ─── SDK call boundary ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs one SDK call under the whole-call budget and translates every SDK failure into a
    /// <see cref="UpvestException"/>. The caller's own cancellation propagates unwrapped.
    /// </summary>
    private async Task<T> CallAsync<T>(string operation, Func<CancellationToken, Task<T>> call, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(CallBudget);
        try
        {
            return await call(cts.Token);
        }
        catch (ResponseDeserializationException ex)
        {
            int status = (int)ex.StatusCode;
            LogFailure(operation, status, ex);
            // A 2xx we could not read is an unknown outcome; a non-2xx means the request was rejected.
            throw new UpvestException("Upvest returned a response that could not be processed.",
                status, callerFault: status is >= 400 and < 500, unknownOutcome: status is >= 200 and < 300, innerException: ex);
        }
        catch (ApiException ex)
        {
            int status = (int)ex.StatusCode;
            LogFailure(operation, status, ex);
            // 401/403/429 are our credentials / scopes / quota — not the caller's fault.
            bool callerFault = status is >= 400 and < 500 and not 401 and not 403 and not 429;
            throw new UpvestException(DescribeApiFailure(status), status, callerFault: callerFault, innerException: ex);
        }
        catch (AuthSchemeException ex)
        {
            // The token request for this operation's scope was refused (or a credential was missing).
            // In this sandbox, operations outside users:* fail here with "not authorised ... scopes".
            _logger.LogWarning("Upvest authentication/scope failure for {Operation}: {Message}", operation, ex.Message);
            throw new UpvestException(
                "The investment provider rejected the request (the client is not authorised for the required scopes).",
                innerException: ex);
        }
        catch (SdkTimeoutException ex)
        {
            _logger.LogWarning("Upvest call {Operation} timed out after {Timeout}.", operation, ex.Timeout);
            throw new UpvestException("The investment provider did not respond in time.", unknownOutcome: true, innerException: ex);
        }
        catch (SdkConnectionException ex)
        {
            _logger.LogWarning(ex, "Upvest call {Operation} could not reach the provider.", operation);
            throw new UpvestException("The investment provider is currently unreachable.", unknownOutcome: true, innerException: ex);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The budget elapsed (not the caller cancelling) — treat as an unsettled timeout.
            _logger.LogWarning("Upvest call {Operation} exceeded the {Budget}s call budget.", operation, CallBudget.TotalSeconds);
            throw new UpvestException("The investment provider did not respond within the allotted time.", unknownOutcome: true);
        }
    }

    private static string DescribeApiFailure(int status) => status switch
    {
        401 or 403 => "The investment provider rejected the request (the client is not authorised for the required scopes).",
        429 => "The investment provider is rate-limiting requests; try again shortly.",
        >= 400 and < 500 => $"The request was rejected by the investment provider ({status}).",
        _ => $"The investment provider returned an error ({status})."
    };

    private void LogFailure(string operation, int status, ApiException ex)
    {
        // Status + provider correlation id only — never the request body (it may carry PII).
        _logger.LogWarning("Upvest call {Operation} failed with {Status}. correlationId={CorrelationId}",
            operation, status, Correlation(ex.Headers));
    }

    private static string? Correlation(HttpResponseHeaders headers)
    {
        foreach (var name in new[] { "upvest-correlation-id", "x-request-id", "x-correlation-id", "request-id" })
        {
            if (headers.TryGetValues(name, out var values))
            {
                foreach (var v in values) return v;
            }
        }
        return null;
    }

    private async Task TryStepAsync(string operation, Func<Task> step)
    {
        try
        {
            await step();
        }
        catch (UpvestException ex)
        {
            // A step of the onboarding chain failed (e.g. a scope not available in this sandbox).
            // Logged and skipped; the investor record is left pending for a later refresh.
            _logger.LogWarning("Investor onboarding step {Operation} did not complete: {Message}", operation, ex.Message);
        }
    }

    /// <summary>Walks an offset/limit paged list to completion, with a hard page cap.</summary>
    private async Task<(IReadOnlyList<T> Items, bool Truncated)> PageAsync<T>(
        Func<int, int, CancellationToken, Task<(IReadOnlyList<T> Data, Meta Meta)>> fetch, CancellationToken cancellationToken)
    {
        var items = new List<T>();
        int offset = 0, pages = 0;
        while (true)
        {
            var (data, meta) = await fetch(offset, PageSize, cancellationToken);
            items.AddRange(data);
            pages++;
            offset += data.Count;
            if (data.Count == 0 || items.Count >= meta.TotalCount) return (items, false);
            if (pages >= MaxPages) return (items, true); // cut short: caller must treat the result as partial
        }
    }

    // ─── value conversions ──────────────────────────────────────────────────────────────────────

    private static ModelPortfolioResult ToModelResult(ModelPortfolio m) =>
        new(m.ModelId, m.Name, m.Funds.Select(f => new FundResult(f.InstrumentId, f.Weight)).ToList());

    private static string MapOrderStatus(string wire) => wire switch
    {
        "FILLED" or "SETTLED" => PortfolioStatuses.Payment.Invested,
        "CANCELLED" => PortfolioStatuses.Payment.Failed,
        _ => PortfolioStatuses.Payment.Pending // NEW / PROCESSING / anything undeclared
    };

    /// <summary>Formats a euro amount as the two-decimal decimal string Upvest expects.</summary>
    private static string FormatMoney(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Formats a percentage weight to the pattern Upvest's allocation weight accepts.</summary>
    private static string FormatWeight(decimal weight) =>
        weight == 100m ? "100" : weight.ToString("0.#####", CultureInfo.InvariantCulture);

    private static decimal ParseDecimal(string value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;

    private static string Up(string code) => code.Trim().ToUpperInvariant();

    private static Nationality ResolveNationality(string code) =>
        Nationality.TryGetKnownValue(Up(code), out var v) && v is not null ? v
            : throw new PortfolioValidationException($"'{code}' is not a recognised ISO 3166-1 alpha-2 nationality code.");

    private static Country ResolveCountry(string code) =>
        Country.TryGetKnownValue(Up(code), out var v) && v is not null ? v
            : throw new PortfolioValidationException($"'{code}' is not a recognised ISO 3166-1 alpha-2 country code.");

    private static BirthCountry ResolveBirthCountry(string code) =>
        BirthCountry.TryGetKnownValue(Up(code), out var v) && v is not null ? v
            : throw new PortfolioValidationException($"'{code}' is not a recognised ISO 3166-1 alpha-2 birth-country code.");

    private static IssuingCountry ResolveIssuingCountry(string code) =>
        IssuingCountry.TryGetKnownValue(Up(code), out var v) && v is not null ? v
            : throw new PortfolioValidationException($"'{code}' is not a recognised ISO 3166-1 alpha-2 issuing-country code.");

    private static Currency ResolveCurrency(string code) =>
        Currency.TryGetKnownValue(Up(code), out var v) && v is not null ? v
            : throw new PortfolioValidationException($"'{code}' is not a supported currency.");

    private static Side15 ResolveBuySide() =>
        Side15.TryGetKnownValue("BUY", out var v) && v is not null ? v
            : throw new InvalidOperationException("The SDK does not declare a BUY order side.");

    private static Type7 ResolveDocumentType(string type) =>
        Up(type).Contains("PASSPORT") ? Type7.Passport : Type7.NationalId;

    private static bool IsUniqueViolation(Exception ex)
    {
        // The app store refuses a duplicate claim (unique key). The in-memory provider surfaces a key
        // clash as ArgumentException/InvalidOperationException; a relational provider as DbUpdateException.
        for (var e = ex; e is not null; e = e.InnerException)
        {
            var name = e.GetType().Name;
            if (name.Contains("DbUpdateException") || e is ArgumentException || e is InvalidOperationException)
                return true;
        }
        return false;
    }
}
