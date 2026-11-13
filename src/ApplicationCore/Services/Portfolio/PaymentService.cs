using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services.Portfolio;

public class PaymentService : IPaymentService
{
    private readonly IPortfolioRepository<Payment> _payments;
    private readonly IPortfolioRepository<Investor> _investors;
    private readonly IUpvestInvestmentGateway _gateway;
    private readonly IAppLogger<PaymentService> _logger;

    public PaymentService(
        IPortfolioRepository<Payment> payments,
        IPortfolioRepository<Investor> investors,
        IUpvestInvestmentGateway gateway,
        IAppLogger<PaymentService> logger)
    {
        _payments = payments;
        _investors = investors;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<PaymentSummary> PayInAsync(string shopperId, decimal amount, string? idempotencyKey, CancellationToken cancellationToken)
    {
        if (amount <= 0m)
        {
            throw new ArgumentException("The amount paid in must be greater than zero.", nameof(amount));
        }

        var investor = await _investors.GetByIdAsync(shopperId, cancellationToken);
        if (investor is null || !investor.IsReadyToInvest || string.IsNullOrEmpty(investor.ChosenModelId))
        {
            throw new InvestorNotReadyException(
                "A pay-in is only possible once Upvest has accepted you as an investor, your account is ready, and you have chosen a model portfolio.");
        }

        // Double-submit protection: when the caller supplies an idempotency key, derive a stable
        // payment id so a retry collides on the store's primary key and returns the original payment.
        var paymentId = idempotencyKey is { Length: > 0 }
            ? DeterministicId(shopperId, idempotencyKey)
            : Guid.NewGuid();

        var payment = new Payment(paymentId, shopperId, amount);
        try
        {
            // Claim before any Upvest call: a racing duplicate is refused here, before money moves.
            await _payments.AddAsync(payment, cancellationToken);
        }
        catch (Exception)
        {
            var existing = await _payments.GetByIdAsync(paymentId, cancellationToken);
            if (existing is not null)
            {
                return ToSummary(existing);
            }
            throw;
        }

        await InvestAsync(payment, investor, cancellationToken);
        await _payments.UpdateAsync(payment, cancellationToken);
        return ToSummary(payment);
    }

    public async Task<IReadOnlyList<PaymentSummary>> ListAsync(string shopperId, CancellationToken cancellationToken)
    {
        var payments = await _payments.ListAsync(new PaymentsByShopperSpec(shopperId), cancellationToken);

        // Status must reflect Upvest — reconcile any still-pending payment on read.
        foreach (var payment in payments.Where(p => p.Status == PaymentStatus.Pending))
        {
            try
            {
                await ReconcileAsync(payment, cancellationToken);
                await _payments.UpdateAsync(payment, cancellationToken);
            }
            catch (UpvestGatewayException ex)
            {
                _logger.LogWarning("Could not reconcile payment {PaymentId} (status={Status}, scopeWall={ScopeWall}).", payment.PaymentId, ex.StatusCode, ex.IsScopeWall);
            }
        }

        return payments.Select(ToSummary).ToList();
    }

    private async Task InvestAsync(Payment payment, Investor investor, CancellationToken cancellationToken)
    {
        var accountGroupId = investor.UpvestAccountGroupId!.Value;
        var accountId = investor.UpvestAccountId!.Value;
        var userId = investor.UpvestUserId!.Value;

        try
        {
            if (payment.UpvestTopupId is null)
            {
                var topupId = await _gateway.CreateTopupAsync(accountGroupId, payment.Amount, payment.TopupIdempotencyKey, cancellationToken);
                payment.SetTopupId(topupId);
                await _payments.UpdateAsync(payment, cancellationToken);
            }

            if (payment.UpvestOrderId is null)
            {
                var orderId = await _gateway.CreateOrderAsync(userId, accountId, payment.Amount, payment.OrderIdempotencyKey, cancellationToken);
                payment.SetOrderId(orderId);
                await _payments.UpdateAsync(payment, cancellationToken);
            }

            await ReconcileAsync(payment, cancellationToken);
        }
        catch (UpvestGatewayException ex) when (ex.IsScopeWall)
        {
            payment.SetNote("Awaiting provider authorisation for payment scopes; the pay-in will settle once granted.");
            _logger.LogWarning("Pay-in paused at scope wall (payment stays pending).");
        }
        catch (UpvestGatewayException ex) when (ex.OutcomeUnknown)
        {
            // The write may still have landed — leave pending and reconcile on a later read/replay.
            payment.SetNote("Pay-in submitted; awaiting confirmation from the provider.");
            _logger.LogWarning("Pay-in outcome unknown; will reconcile on next read.");
        }
        catch (UpvestGatewayException ex)
        {
            payment.SetStatus(PaymentStatus.Failed);
            payment.SetNote("The provider rejected the pay-in.");
            _logger.LogWarning("Pay-in failed at the provider (status={Status}).", ex.StatusCode);
        }
    }

    private async Task ReconcileAsync(Payment payment, CancellationToken cancellationToken)
    {
        var topupStatus = payment.UpvestTopupId is Guid topupId
            ? await _gateway.GetTopupStatusAsync(topupId, cancellationToken)
            : UpvestLegStatus.Pending;

        if (topupStatus == UpvestLegStatus.Failed)
        {
            payment.SetStatus(PaymentStatus.Failed);
            return;
        }

        if (payment.UpvestOrderId is not Guid orderId)
        {
            payment.SetStatus(PaymentStatus.Pending);
            return;
        }

        var orderStatus = await _gateway.GetOrderStatusAsync(orderId, cancellationToken);
        payment.SetStatus(orderStatus switch
        {
            UpvestLegStatus.Settled => PaymentStatus.Invested,
            UpvestLegStatus.Failed => PaymentStatus.Failed,
            _ => PaymentStatus.Pending
        });
    }

    private static PaymentSummary ToSummary(Payment payment) =>
        new(payment.PaymentId, payment.Amount, payment.Status);

    private static Guid DeterministicId(string shopperId, string idempotencyKey)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes($"{shopperId}|{idempotencyKey}"));
        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, 16);
        return new Guid(guidBytes);
    }
}
