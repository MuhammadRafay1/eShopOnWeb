using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Default <see cref="IPortfolioService"/>. Each per-shopper method keys everything on the shopper's username and
/// only ever reads or writes that shopper's own records. Onboarding and models are resumable: progress is
/// persisted after every successful Upvest write, so a later call continues where a provider failure left off
/// without creating duplicate resources (the persisted idempotency keys make a replay safe).
/// </summary>
public class PortfolioService : IPortfolioService
{
    private const decimal RequiredWeightTotal = 100m;

    private readonly IRepository<Investor> _investors;
    private readonly IRepository<ModelPortfolio> _models;
    private readonly IRepository<PortfolioPayment> _payments;
    private readonly IRepository<Realignment> _realignments;
    private readonly IUpvestPortfolioGateway _gateway;
    private readonly IAppLogger<PortfolioService> _logger;

    public PortfolioService(
        IRepository<Investor> investors,
        IRepository<ModelPortfolio> models,
        IRepository<PortfolioPayment> payments,
        IRepository<Realignment> realignments,
        IUpvestPortfolioGateway gateway,
        IAppLogger<PortfolioService> logger)
    {
        _investors = investors;
        _models = models;
        _payments = payments;
        _realignments = realignments;
        _gateway = gateway;
        _logger = logger;
    }

    // --- Flow 1 -------------------------------------------------------------------------------------------

    public async Task<Investor> RegisterInvestorAsync(string shopperUserName, InvestorRegistration registration, CancellationToken cancellationToken)
    {
        var investor = await GetOrCreateInvestorClaimAsync(shopperUserName, cancellationToken);

        // Resume from wherever onboarding last got to. Each write uses its persisted idempotency key, so a
        // replay (within this call or a later one) deduplicates at Upvest rather than creating a second resource.
        if (!investor.UpvestUserId.HasValue)
        {
            var userId = await _gateway.CreateInvestorUserAsync(registration, investor.UserIdempotencyKey, cancellationToken);
            investor.SetUpvestUserId(userId);
            await _investors.UpdateAsync(investor, cancellationToken);
            _logger.LogInformation("Investor onboarding: created Upvest user {UserId}.", userId);
        }

        if (!investor.AccountGroupId.HasValue)
        {
            var accountGroupId = await _gateway.CreateAccountGroupAsync(investor.UpvestUserId!.Value, investor.AccountGroupIdempotencyKey, cancellationToken);
            investor.SetAccountGroupId(accountGroupId);
            await _investors.UpdateAsync(investor, cancellationToken);
            _logger.LogInformation("Investor onboarding: created account group {AccountGroupId}.", accountGroupId);
        }

        if (!investor.AccountId.HasValue)
        {
            var accountId = await _gateway.CreatePortfolioAccountAsync(investor.UpvestUserId!.Value, investor.AccountGroupId!.Value, investor.AccountIdempotencyKey, cancellationToken);
            investor.SetAccountId(accountId);
            await _investors.UpdateAsync(investor, cancellationToken);
            _logger.LogInformation("Investor onboarding: created portfolio account {AccountId}.", accountId);
        }

        // Best-effort final status read-back: onboarding has already succeeded, so a read blip must not fail it.
        try
        {
            await RefreshInvestorStatusAsync(investor, cancellationToken);
        }
        catch (Microsoft.eShopWeb.ApplicationCore.Exceptions.UpvestIntegrationException ex)
        {
            _logger.LogWarning("Could not confirm investor status after onboarding: {Message}", ex.Message);
        }

        return investor;
    }

    public async Task<Investor?> GetInvestorAsync(string shopperUserName, CancellationToken cancellationToken)
    {
        var investor = await _investors.GetByIdAsync(shopperUserName, cancellationToken);
        if (investor is null)
        {
            return null;
        }

        if (investor.UpvestUserId.HasValue)
        {
            try
            {
                await RefreshInvestorStatusAsync(investor, cancellationToken);
            }
            catch (Microsoft.eShopWeb.ApplicationCore.Exceptions.UpvestIntegrationException ex)
            {
                // A read endpoint must not hard-fail on a provider blip: fall back to the last-known status.
                // (We still know the sign-up cannot be 'active' while onboarding is incomplete.)
                _logger.LogWarning("Could not refresh investor status from the provider: {Message}", ex.Message);
            }
        }

        return investor;
    }

    private async Task RefreshInvestorStatusAsync(Investor investor, CancellationToken cancellationToken)
    {
        if (!investor.UpvestUserId.HasValue)
        {
            return;
        }

        var status = await _gateway.GetInvestorStatusAsync(
            investor.UpvestUserId.Value, investor.AccountGroupId, investor.AccountId, cancellationToken);
        investor.SetStatus(status);
        await _investors.UpdateAsync(investor, cancellationToken);
    }

    private async Task<Investor> GetOrCreateInvestorClaimAsync(string shopperUserName, CancellationToken cancellationToken)
    {
        var existing = await _investors.GetByIdAsync(shopperUserName, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var investor = new Investor(shopperUserName);
        try
        {
            return await _investors.AddAsync(investor, cancellationToken);
        }
        catch (Exception) // duplicate primary key under a concurrent sign-up — the store rejects the second claim
        {
            var reloaded = await _investors.GetByIdAsync(shopperUserName, cancellationToken);
            if (reloaded is not null)
            {
                return reloaded;
            }

            throw;
        }
    }

    // --- Flow 2 -------------------------------------------------------------------------------------------

    public async Task<ModelPortfolio> UpsertModelAsync(string modelId, string name, IReadOnlyList<FundWeight> funds, CancellationToken cancellationToken)
    {
        ValidateModel(name, funds);

        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByIdSpec(modelId), cancellationToken);
        var isNew = model is null;
        if (model is null)
        {
            model = new ModelPortfolio(modelId, name);
        }

        model.Replace(name, funds.Select(f => new ModelPortfolioFund(f.InstrumentId, f.Weight)));

        // Persist operator intent first, so GET models reflects it even if the Upvest write below is refused.
        if (isNew)
        {
            await _models.AddAsync(model, cancellationToken);
        }
        else
        {
            await _models.UpdateAsync(model, cancellationToken);
        }

        if (!model.AllocationId.HasValue)
        {
            var allocationId = await _gateway.CreateModelAllocationAsync(name, funds, model.AllocationIdempotencyKey, cancellationToken);
            model.SetAllocationId(allocationId);
            await _models.UpdateAsync(model, cancellationToken);
            _logger.LogInformation("Model {ModelId} mapped to Upvest allocation {AllocationId}.", modelId, allocationId);
        }
        else
        {
            await _gateway.UpdateModelAllocationAsync(model.AllocationId.Value, name, funds, model.AllocationIdempotencyKey, cancellationToken);
            _logger.LogInformation("Model {ModelId} allocation {AllocationId} updated.", modelId, model.AllocationId.Value);
        }

        return model;
    }

    public async Task<IReadOnlyList<ModelPortfolio>> ListModelsAsync(CancellationToken cancellationToken)
    {
        // The app store is authoritative for the model-id namespace and the funds/weights the operator set;
        // these are exactly what was sent to Upvest. (Upvest's allocation list requires a read scope this
        // sandbox client lacks; the app store serves the view without it.)
        return await _models.ListAsync(new ModelPortfoliosWithFundsSpec(), cancellationToken);
    }

    public async Task<Investor> ChooseModelAsync(string shopperUserName, string modelId, CancellationToken cancellationToken)
    {
        var investor = await _investors.GetByIdAsync(shopperUserName, cancellationToken)
            ?? throw new PortfolioRequestException("You are not registered as an investor.", 409);

        if (!investor.AccountId.HasValue)
        {
            throw new PortfolioRequestException("Your investor account is not ready yet.", 409);
        }

        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByIdSpec(modelId), cancellationToken)
            ?? throw new PortfolioRequestException($"Model '{modelId}' does not exist.", 404);

        if (!model.AllocationId.HasValue)
        {
            throw new PortfolioRequestException($"Model '{modelId}' is not available at the investment provider yet.", 409);
        }

        await _gateway.SetFollowedModelAsync(
            investor.AccountId.Value, model.AllocationId.Value, investor.HasChosenModel, investor.ConfigurationIdempotencyKey, cancellationToken);

        investor.ChooseModel(modelId, model.AllocationId.Value);
        await _investors.UpdateAsync(investor, cancellationToken);
        _logger.LogInformation("Investor chose model {ModelId}.", modelId);
        return investor;
    }

    private static void ValidateModel(string name, IReadOnlyList<FundWeight> funds)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new PortfolioRequestException("A model name is required.", 400);
        }

        if (funds is null || funds.Count == 0)
        {
            throw new PortfolioRequestException("A model must have at least one fund.", 400);
        }

        if (funds.Any(f => string.IsNullOrWhiteSpace(f.InstrumentId)))
        {
            throw new PortfolioRequestException("Every fund must carry an instrument id (ISIN).", 400);
        }

        if (funds.Any(f => !System.Text.RegularExpressions.Regex.IsMatch(f.InstrumentId, "^[A-Z]{2}[A-Z0-9]{9}[0-9]$")))
        {
            throw new PortfolioRequestException("Every fund instrument id must be a valid ISIN.", 400);
        }

        if (funds.Any(f => f.Weight <= 0m))
        {
            throw new PortfolioRequestException("Every fund weight must be positive.", 400);
        }

        var duplicate = funds.GroupBy(f => f.InstrumentId).Any(g => g.Count() > 1);
        if (duplicate)
        {
            throw new PortfolioRequestException("A fund may appear only once in a model.", 400);
        }

        var total = funds.Sum(f => f.Weight);
        if (total != RequiredWeightTotal)
        {
            throw new PortfolioRequestException($"Fund weights must add up to exactly 100; they add up to {total}.", 400);
        }
    }

    // --- Flow 3 -------------------------------------------------------------------------------------------

    public async Task<PortfolioPayment> PayInAsync(string shopperUserName, decimal amount, Guid? idempotencyKey, CancellationToken cancellationToken)
    {
        if (amount <= 0m)
        {
            throw new PortfolioRequestException("Amount must be a positive number of euros.", 400);
        }

        var investor = await _investors.GetByIdAsync(shopperUserName, cancellationToken)
            ?? throw new PortfolioRequestException("You are not registered as an investor.", 409);

        if (investor.Status != InvestorStatus.Active || !investor.AccountId.HasValue)
        {
            throw new PortfolioRequestException("You must be an accepted investor before paying in.", 409);
        }

        if (!investor.HasChosenModel)
        {
            throw new PortfolioRequestException("Choose a model portfolio before paying in.", 409);
        }

        // Claim: the payment row is the duplicate-submit guard. When the caller supplies an idempotency key it
        // is the primary key, so a re-submit is rejected by the store on insert and the first payment returned.
        var paymentId = idempotencyKey ?? Guid.NewGuid();
        var (payment, isDuplicate) = await ClaimPaymentAsync(paymentId, shopperUserName, amount, cancellationToken);
        if (isDuplicate)
        {
            _logger.LogInformation("Duplicate pay-in {PaymentId} ignored; returning the existing payment.", paymentId);
            return payment;
        }

        var iban = investor.VirtualBankAccountIban;
        if (string.IsNullOrEmpty(iban))
        {
            iban = await _gateway.EnsureVirtualBankAccountIbanAsync(investor.AccountGroupId!.Value, investor.VirtualBankAccountIdempotencyKey, cancellationToken);
            investor.SetVirtualBankAccountIban(iban);
            await _investors.UpdateAsync(investor, cancellationToken);
        }

        await _gateway.CreditCashAsync(iban, amount, payment.PaymentId.ToString(), payment.CashCreditIdempotencyKey, cancellationToken);

        var order = await _gateway.PlaceBuyOrderAsync(investor.UpvestUserId!.Value, investor.AccountId.Value, amount, payment.OrderIdempotencyKey, cancellationToken);
        payment.SetUpvestOrderId(order.OrderId);
        payment.SetStatus(order.Status);
        await _payments.UpdateAsync(payment, cancellationToken);
        _logger.LogInformation("Pay-in {PaymentId} placed Upvest order {OrderId} (status {Status}).", payment.PaymentId, order.OrderId, order.Status);

        return payment;
    }

    public async Task<IReadOnlyList<PortfolioPayment>> GetPaymentsAsync(string shopperUserName, CancellationToken cancellationToken)
    {
        var payments = await _payments.ListAsync(new PaymentsByShopperSpec(shopperUserName), cancellationToken);

        foreach (var payment in payments.Where(p => p.UpvestOrderId.HasValue && p.Status == PaymentStatus.Pending))
        {
            var status = await _gateway.GetOrderStatusAsync(payment.UpvestOrderId!.Value, cancellationToken);
            if (status != payment.Status)
            {
                payment.SetStatus(status);
                await _payments.UpdateAsync(payment, cancellationToken);
            }
        }

        return payments;
    }

    private async Task<(PortfolioPayment payment, bool isDuplicate)> ClaimPaymentAsync(Guid paymentId, string shopperUserName, decimal amount, CancellationToken cancellationToken)
    {
        var existing = await _payments.GetByIdAsync(paymentId, cancellationToken);
        if (existing is not null)
        {
            return (existing, true);
        }

        var payment = new PortfolioPayment(paymentId, shopperUserName, amount);
        try
        {
            var saved = await _payments.AddAsync(payment, cancellationToken);
            return (saved, false);
        }
        catch (Exception) // concurrent duplicate claim rejected by the store
        {
            var reloaded = await _payments.GetByIdAsync(paymentId, cancellationToken);
            if (reloaded is not null)
            {
                return (reloaded, true);
            }

            throw;
        }
    }

    // --- Flow 4 -------------------------------------------------------------------------------------------

    public async Task<Realignment> TriggerRealignmentAsync(string modelId, CancellationToken cancellationToken)
    {
        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByIdSpec(modelId), cancellationToken)
            ?? throw new PortfolioRequestException($"Model '{modelId}' does not exist.", 404);

        if (!model.AllocationId.HasValue)
        {
            throw new PortfolioRequestException($"Model '{modelId}' is not available at the investment provider yet.", 409);
        }

        var idempotencyKey = Guid.NewGuid();
        var executionId = await _gateway.TriggerRealignmentAsync(model.AllocationId.Value, idempotencyKey, cancellationToken);

        var realignment = new Realignment(executionId, modelId, model.AllocationId.Value);
        var status = await _gateway.GetRealignmentStatusAsync(executionId, cancellationToken);
        realignment.SetStatus(status);
        await _realignments.AddAsync(realignment, cancellationToken);
        _logger.LogInformation("Realignment {RealignmentId} triggered for model {ModelId}.", executionId, modelId);

        return realignment;
    }

    public async Task<RealignmentStatus> GetRealignmentStatusAsync(Guid realignmentId, CancellationToken cancellationToken)
    {
        var status = await _gateway.GetRealignmentStatusAsync(realignmentId, cancellationToken);

        var stored = await _realignments.GetByIdAsync(realignmentId, cancellationToken);
        if (stored is not null && stored.Status != status)
        {
            stored.SetStatus(status);
            await _realignments.UpdateAsync(stored, cancellationToken);
        }

        return status;
    }

    // --- Flow 5 -------------------------------------------------------------------------------------------

    public async Task<PortfolioView> GetPortfolioAsync(string shopperUserName, CancellationToken cancellationToken)
    {
        var investor = await _investors.GetByIdAsync(shopperUserName, cancellationToken)
            ?? throw new PortfolioRequestException("You are not registered as an investor.", 409);

        if (!investor.AccountId.HasValue)
        {
            throw new PortfolioRequestException("Your investor account is not ready yet.", 409);
        }

        var allocationId = await _gateway.GetFollowedAllocationIdAsync(investor.AccountId.Value, cancellationToken);
        var modelId = allocationId.HasValue ? await ResolveModelIdAsync(allocationId.Value, cancellationToken) : investor.ChosenModelId;

        var holdings = await _gateway.GetPortfolioHoldingsAsync(investor.AccountId.Value, cancellationToken);

        var paidIn = (await _payments.ListAsync(new PaymentsByShopperSpec(shopperUserName), cancellationToken))
            .Where(p => p.Status == PaymentStatus.Invested)
            .Sum(p => p.Amount);

        return new PortfolioView(modelId, holdings.Holdings, holdings.TotalValue, paidIn, holdings.Currency);
    }

    private async Task<string?> ResolveModelIdAsync(Guid allocationId, CancellationToken cancellationToken)
    {
        var models = await _models.ListAsync(cancellationToken);
        return models.FirstOrDefault(m => m.AllocationId == allocationId)?.ModelId;
    }
}
