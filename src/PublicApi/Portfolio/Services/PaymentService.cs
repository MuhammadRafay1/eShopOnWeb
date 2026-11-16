using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.Services;

public interface IPaymentService
{
    Task<PaymentResult> PayInAsync(string shopperId, decimal amount, CancellationToken ct);
    Task<IReadOnlyList<PaymentResult>> ListAsync(string shopperId, CancellationToken ct);
}

/// <summary>
/// Pays money into an investor's account and invests it across their model's funds. The money is
/// credited at Upvest (a top-up, settled in the sandbox by a simulated inbound transfer) and then a
/// portfolio BUY order invests it. The app-side <see cref="Payment"/> row is claimed first and holds
/// the idempotency keys so each write is safe to retry and unknown outcomes reconcile on the next read.
/// </summary>
public sealed class PaymentService : IPaymentService
{
    private readonly IRepository<Payment> _payments;
    private readonly IRepository<Investor> _investors;
    private readonly IUpvestInvestmentGateway _gateway;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IRepository<Payment> payments,
        IRepository<Investor> investors,
        IUpvestInvestmentGateway gateway,
        ILogger<PaymentService> logger)
    {
        _payments = payments;
        _investors = investors;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<PaymentResult> PayInAsync(string shopperId, decimal amount, CancellationToken ct)
    {
        if (amount <= 0)
            throw new PortfolioValidationException("Amount must be greater than zero.");

        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperIdSpecification(shopperId), ct);
        if (investor is null || !investor.CanInvest)
            throw new PortfolioValidationException("You must be an accepted investor with a ready account before paying in.", 409);
        if (!investor.SelectedAllocationId.HasValue)
            throw new PortfolioValidationException("You must choose a model portfolio before paying in.", 409);

        // Claim the payment row (with its idempotency keys) before any provider call.
        var payment = new Payment(Guid.NewGuid(), shopperId, amount);
        payment = await _payments.AddAsync(payment, ct);

        try
        {
            // 1. Credit the account group at Upvest.
            var topup = await _gateway.CreateTopupAsync(investor.UpvestAccountGroupId!.Value, amount, payment.TopupKey, ct);
            payment.SetTopup(topup.TopupId);
            await _payments.UpdateAsync(payment, ct);

            // 2. Sandbox: simulate the inbound transfer that settles the top-up.
            await _gateway.SimulateInboundTransferAsync(amount, payment.ClientReference, topup.SettlementReference, payment.BankTransferKey, ct);

            // 3. Invest the cash across the funds of the investor's model (BUY).
            var orderId = await _gateway.PlaceBuyOrderAsync(investor.UpvestUserId!.Value, investor.UpvestAccountId!.Value, amount, payment.OrderKey, ct);
            payment.SetOrder(orderId);
            await _payments.UpdateAsync(payment, ct);
        }
        catch (UpvestGatewayException ex)
        {
            // The payment row is retained: a known provider rejection surfaces, while an unknown
            // outcome is left 'pending' to be settled by the next GET /payments read.
            _logger.LogWarning(ex, "Pay-in {PaymentId} did not complete (outcomeUnknown={Unknown}).", payment.PaymentId, ex.OutcomeUnknown);
            if (!ex.OutcomeUnknown)
                throw;
        }

        var state = await SafeStateAsync(payment, ct);
        return new PaymentResult(payment.PaymentId, payment.Amount, state);
    }

    public async Task<IReadOnlyList<PaymentResult>> ListAsync(string shopperId, CancellationToken ct)
    {
        var payments = await _payments.ListAsync(new PaymentsByShopperIdSpecification(shopperId), ct);
        var results = new List<PaymentResult>(payments.Count);
        foreach (var payment in payments)
        {
            var state = await SafeStateAsync(payment, ct);
            results.Add(new PaymentResult(payment.PaymentId, payment.Amount, state));
        }
        return results;
    }

    private async Task<PaymentState> SafeStateAsync(Payment payment, CancellationToken ct)
    {
        try
        {
            var state = await _gateway.GetPaymentStateAsync(payment.UpvestTopupId, payment.UpvestOrderId, ct);
            payment.UpdateStatusCache(state.ToString().ToLowerInvariant());
            await _payments.UpdateAsync(payment, ct);
            return state;
        }
        catch (UpvestGatewayException ex)
        {
            _logger.LogWarning(ex, "Could not read payment status from Upvest; using last-known status.");
            return ParseState(payment.StatusCache);
        }
    }

    private static PaymentState ParseState(string cache) => cache switch
    {
        "invested" => PaymentState.Invested,
        "failed" => PaymentState.Failed,
        _ => PaymentState.Pending,
    };
}
