using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Microsoft.Extensions.Logging;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Models;
using UpvestInvestmentApi.Models.Enums;
using ReqTopUps = UpvestInvestmentApi.Requests.TopUps;
using ReqPortfolios = UpvestInvestmentApi.Requests.Portfolios;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio.Services;

/// <summary>
/// Flow 3 — paying in. Each pay-in credits the investor's Upvest account (a top-up) and invests it across their
/// model's funds in its weights (a portfolio BUY order). The payment record is claimed before any provider call,
/// carries the two idempotency keys, and its status is reconciled from Upvest on every read.
/// </summary>
public sealed class PaymentService : IPaymentService
{
    private readonly UpvestInvestmentApiClient _client;
    private readonly PortfolioContext _db;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(UpvestInvestmentApiClient client, PortfolioContext db, ILogger<PaymentService> logger)
    {
        _client = client;
        _db = db;
        _logger = logger;
    }

    public async Task<PaymentResult> PayInAsync(string shopperId, decimal amount, CancellationToken ct)
    {
        if (amount <= 0m)
            throw new PortfolioValidationException("The amount to pay in must be greater than zero.");
        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);

        var investor = await _db.Investors.FirstOrDefaultAsync(i => i.ShopperId == shopperId, ct);
        if (investor?.AccountId is null || investor.AccountGroupId is null || investor.UpvestUserId is null)
            throw new PortfolioConflictException("You must complete investor sign-up before paying in.");
        if (investor.ChosenModelId is null)
            throw new PortfolioConflictException("You must choose a model portfolio before paying in.");

        // Claim the payment before any provider call, carrying both idempotency keys for safe replay/reconcile.
        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            ShopperId = shopperId,
            Amount = amount,
            Status = PaymentStatus.Pending,
            TopUpIdempotencyKey = Guid.NewGuid(),
            OrderIdempotencyKey = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(ct);

        try
        {
            var cash = UpvestMapping.CashAmount(amount);

            var topup = await UpvestCall.WriteAsync(() => _client.TopUps.CreateTopup(
                new ReqTopUps.CreateTopupRequest
                {
                    IdempotencyKey = payment.TopUpIdempotencyKey,
                    Body = new PaymentsTopUpCreateRequest
                    {
                        AccountGroupId = investor.AccountGroupId.Value,
                        CashAmount = cash,
                        Currency = Currency.Eur
                    }
                }, cancellationToken: ct), "CreateTopup", _logger);
            payment.TopUpId = topup.Id;
            await _db.SaveChangesAsync(ct);

            var order = await UpvestCall.WriteAsync(() => _client.Portfolios.CreatePortfoliosOrder(
                new ReqPortfolios.CreatePortfoliosOrderRequest
                {
                    IdempotencyKey = payment.OrderIdempotencyKey,
                    Body = new PortfoliosOrderPlaceRequest
                    {
                        UserId = investor.UpvestUserId.Value,
                        AccountId = investor.AccountId.Value,
                        CashAmount = cash,
                        Currency = Currency.Eur,
                        Side = Side15.Buy
                    }
                }, cancellationToken: ct), "CreatePortfoliosOrder", _logger);
            payment.OrderId = order.Id;
            payment.Status = Combine(topup.Status, order.Status);
            await _db.SaveChangesAsync(ct);
        }
        catch (PortfolioUnknownOutcomeException)
        {
            // A write may have landed; leave it pending so the status read reconciles it from Upvest.
            _logger.LogWarning("Pay-in {PaymentId} outcome unknown; left pending for reconciliation.", payment.PaymentId);
        }
        catch (PortfolioProviderException ex)
        {
            // The provider refused the pay-in; it did not invest. Report it as failed (status reflects Upvest).
            _logger.LogWarning("Pay-in {PaymentId} failed at the provider (status {Status}).", payment.PaymentId, (int?)ex.StatusCode);
            payment.Status = PaymentStatus.Failed;
            await _db.SaveChangesAsync(ct);
        }

        return ToResult(payment);
    }

    public async Task<IReadOnlyList<PaymentResult>> ListPaymentsAsync(string shopperId, CancellationToken ct)
    {
        var payments = await _db.Payments
            .Where(p => p.ShopperId == shopperId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        var changed = false;
        foreach (var p in payments.Where(p => p.Status == PaymentStatus.Pending))
            changed |= await ReconcileAsync(p, ct);
        if (changed) await _db.SaveChangesAsync(ct);

        return payments.Select(ToResult).ToList();
    }

    /// <summary>Re-reads the top-up and order at Upvest to settle a still-pending payment. Best-effort.</summary>
    private async Task<bool> ReconcileAsync(Payment p, CancellationToken ct)
    {
        try
        {
            Status29? topupStatus = null;
            if (p.TopUpId is not null)
            {
                var topup = await UpvestCall.ReadAsync(() => _client.TopUps.RetrieveTopup(
                    new ReqTopUps.RetrieveTopupRequest { TopupId = p.TopUpId.Value }, cancellationToken: ct),
                    "RetrieveTopup", _logger);
                topupStatus = topup.Status;
            }

            if (p.OrderId is not null)
            {
                var order = await UpvestCall.ReadAsync(() => _client.Portfolios.RetrievePortfoliosOrder(
                    new ReqPortfolios.RetrievePortfoliosOrderRequest { PortfolioOrderId = p.OrderId.Value }, cancellationToken: ct),
                    "RetrievePortfoliosOrder", _logger);
                var settled = Combine(topupStatus, order.Status);
                if (settled != p.Status) { p.Status = settled; return true; }
            }
            return false;
        }
        catch (PortfolioProviderException)
        {
            return false; // can't reconcile right now — leave as pending
        }
    }

    private static PaymentStatus Combine(Status29? topup, Status65 order)
    {
        if (topup is not null && topup == Status29.Cancelled) return PaymentStatus.Failed;
        if (order == Status65.Filled || order == Status65.Settled) return PaymentStatus.Invested;
        if (order == Status65.Cancelled) return PaymentStatus.Failed;
        return PaymentStatus.Pending;
    }

    private static PaymentResult ToResult(Payment p) => new(p.PaymentId, p.Amount, p.Status);
}
