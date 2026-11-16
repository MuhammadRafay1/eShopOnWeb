using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;
using Microsoft.Extensions.Logging;
using UpvestInvestmentApi.Core.Exceptions;
using UpvestInvestmentApi.Models;
using UpvestInvestmentApi.Models.Enums;
using UpvestInvestmentApi.Requests.Portfolios;
using UpvestInvestmentApi.Requests.TopUps;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio;

public partial class UpvestPortfolioService
{
    // Flow 3 — paying in ----------------------------------------------------------------------------

    public async Task<PaymentResult> PayInAsync(string shopper, decimal amount, CancellationToken cancellationToken)
    {
        if (amount <= 0m)
        {
            throw new PortfolioValidationException("The pay-in amount must be greater than zero.");
        }

        var investor = await _repository.GetInvestorAsync(shopper, cancellationToken);
        if (investor is null)
        {
            throw new InvestorNotFoundException();
        }

        // The payment is refused unless Upvest has accepted the investor and a model has been chosen.
        await RefreshInvestorStatusAsync(investor, cancellationToken);
        if (investor.Status != InvestorSignupStatus.Active)
        {
            throw new InvestorNotReadyException("The investor has not been accepted by Upvest yet, so no pay-in is allowed.");
        }
        if (string.IsNullOrEmpty(investor.ChosenModelId))
        {
            throw new InvestorNotReadyException("The investor has not chosen a model portfolio yet, so no pay-in is allowed.");
        }
        if (investor.UpvestAccountGroupId is not Guid accountGroupId || investor.UpvestAccountId is not Guid accountId)
        {
            throw new InvestorNotReadyException("The investor's Upvest account is not ready, so no pay-in is allowed.");
        }

        // Claim first: insert the payment record before any Upvest call, so a concurrent double-submit of the
        // same payment id is rejected by the primary key. The id doubles as the order's idempotency key.
        var paymentId = Guid.NewGuid();
        var payment = new PaymentRecord
        {
            PaymentId = paymentId,
            Shopper = shopper,
            Amount = amount,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        if (!await _repository.TryClaimPaymentAsync(payment, cancellationToken))
        {
            throw new PortfolioValidationException("A payment with the same id already exists.");
        }

        var cashAmount = FormatMoney(amount);

        // Credit the investor's account group (sandbox pay-in mechanism).
        await CallAsync("CreateTopup", c => _client.TopUps.CreateTopup(
            new CreateTopupRequest
            {
                IdempotencyKey = DeterministicKey($"topup:{paymentId}"),
                Body = new PaymentsTopUpCreateRequest
                {
                    AccountGroupId = accountGroupId,
                    CashAmount = cashAmount,
                    Currency = CurrencyEnum
                }
            }, cancellationToken: c), cancellationToken);

        // Invest the cash across the model's funds via a portfolio BUY order.
        try
        {
            var order = await CallAsync("CreatePortfoliosOrder", c => _client.Portfolios.CreatePortfoliosOrder(
                new CreatePortfoliosOrderRequest
                {
                    IdempotencyKey = paymentId, // the real, caller-derived idempotency key for this order
                    Body = new PortfoliosOrderPlaceRequest
                    {
                        UserId = investor.UpvestUserId,
                        AccountId = accountId,
                        CashAmount = cashAmount,
                        Currency = CurrencyEnum,
                        Side = Side15.Buy
                    }
                }, cancellationToken: c), cancellationToken);

            payment.UpvestOrderId = order.Id;
            payment.Status = MapOrderStatus(order.Status);
            await _repository.UpdatePaymentAsync(payment, cancellationToken);
        }
        catch (UpvestApiException ex) when (ex.InnerException is SdkConnectionException)
        {
            // Unknown outcome: the order may still have reached Upvest. Re-read and settle before returning.
            var reconciled = await TryReconcileOrderAsync(accountId, cashAmount, payment.CreatedAt, cancellationToken);
            if (reconciled is not null)
            {
                payment.UpvestOrderId = reconciled.Id;
                payment.Status = MapOrderStatus(reconciled.Status);
                await _repository.UpdatePaymentAsync(payment, cancellationToken);
                _logger.LogInformation("Reconciled pay-in {PaymentId} to Upvest order {OrderId} after a transport failure.", paymentId, reconciled.Id);
            }
            else
            {
                // Could not confirm — leave the payment PENDING (unknown), not failed; a later GET re-reads it.
                _logger.LogWarning("Pay-in {PaymentId} outcome is unknown after a transport failure; left pending for reconciliation.", paymentId);
            }
        }

        return new PaymentResult(payment.PaymentId, payment.Amount, payment.Status);
    }

    public async Task<IReadOnlyList<PaymentResult>> ListPaymentsAsync(string shopper, CancellationToken cancellationToken)
    {
        var payments = await _repository.ListPaymentsAsync(shopper, cancellationToken);

        var results = new List<PaymentResult>(payments.Count);
        foreach (var payment in payments)
        {
            // Re-read any still-pending order so the reported status reflects Upvest's current outcome.
            if (payment.Status == PaymentStatus.Pending && payment.UpvestOrderId is Guid orderId)
            {
                try
                {
                    var order = await CallAsync("RetrievePortfoliosOrder", c => _client.Portfolios.RetrievePortfoliosOrder(
                        new RetrievePortfoliosOrderRequest { PortfolioOrderId = orderId }, cancellationToken: c), cancellationToken);

                    var refreshed = MapOrderStatus(order.Status);
                    if (refreshed != payment.Status)
                    {
                        payment.Status = refreshed;
                        await _repository.UpdatePaymentAsync(payment, cancellationToken);
                    }
                }
                catch (UpvestApiException ex) when (IsAuthorizationFailure(ex))
                {
                    _logger.LogWarning("Cannot refresh payment {PaymentId}: the Upvest client lacks the required scope ({Status}).", payment.PaymentId, ex.ProviderStatusCode);
                }
            }

            results.Add(new PaymentResult(payment.PaymentId, payment.Amount, payment.Status));
        }

        return results;
    }

    private async Task<PortfoliosOrder?> TryReconcileOrderAsync(
        Guid accountId, string cashAmount, DateTimeOffset since, CancellationToken cancellationToken)
    {
        try
        {
            var response = await CallAsync("ListPortfoliosOrders", c => _client.Portfolios.ListPortfoliosOrders(
                new ListPortfoliosOrdersRequest { AccountId = accountId, Limit = 100 }, cancellationToken: c), cancellationToken);

            return response.Data
                .Where(o => o.CashAmount == cashAmount && o.CreatedAt >= since.AddMinutes(-5))
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefault();
        }
        catch (UpvestApiException)
        {
            return null; // reconciliation itself failed; outcome remains unknown.
        }
    }

    private static PaymentStatus MapOrderStatus(Status65 status)
    {
        if (status == Status65.Filled || status == Status65.Settled)
        {
            return PaymentStatus.Invested;
        }
        if (status == Status65.Cancelled)
        {
            return PaymentStatus.Failed;
        }
        return PaymentStatus.Pending; // NEW / PROCESSING / unknown
    }
}
