using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.PublicApi.Portfolio.Data;
using Microsoft.eShopWeb.PublicApi.Portfolio.Domain;
using Microsoft.eShopWeb.PublicApi.Portfolio.Upvest;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.Services;

/// <summary>A pay-in as returned to the caller.</summary>
public sealed record PaymentResult(Guid PaymentId, decimal Amount, string Status);

/// <summary>
/// Flow 3 — paying in. Refuses a pay-in unless the caller is an accepted investor with a model chosen, then credits
/// cash (top-up) and invests it across the model's funds (portfolio BUY order). Status is reconciled from the order.
/// </summary>
public sealed class PaymentService
{
    private readonly PortfolioContext _db;
    private readonly IUpvestGateway _gateway;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(PortfolioContext db, IUpvestGateway gateway, ILogger<PaymentService> logger)
    {
        _db = db;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<PaymentResult> PayInAsync(string shopperId, decimal amount, CancellationToken ct)
    {
        if (amount <= 0m)
            throw new PortfolioRequestException("Amount must be greater than zero.", 400);

        var investor = await _db.Investors.FirstOrDefaultAsync(i => i.ShopperId == shopperId, ct)
            ?? throw new PortfolioRequestException("You are not registered as an investor.", 400);

        if (investor.Status != InvestorStatus.Active || investor.AccountId is null || investor.AccountGroupId is null || investor.UpvestUserId is null)
            throw new PortfolioRequestException("Your investor account is not ready to accept payments yet.", 409);
        if (investor.ChosenModelId is null)
            throw new PortfolioRequestException("Choose a model portfolio before paying in.", 409);

        // Claim first: the payment row (its own primary key) is written before any provider call.
        var payment = new PaymentRecord
        {
            PaymentId = Guid.NewGuid(),
            ShopperId = shopperId,
            Amount = Math.Round(amount, 2, MidpointRounding.ToEven),
            Status = PaymentStatus.Pending,
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(ct);

        try
        {
            var topup = await _gateway.CreateTopupAsync(investor.AccountGroupId.Value, payment.Amount, payment.TopupIdempotencyKey, ct);
            payment.UpvestTopupId = topup.TopupId;
            await _db.SaveChangesAsync(ct);

            var order = await _gateway.CreateBuyOrderAsync(investor.UpvestUserId.Value, investor.AccountId.Value, payment.Amount, payment.OrderIdempotencyKey, ct);
            payment.UpvestOrderId = order.OrderId;
            payment.Status = MapOrderStatus(order.Status);
            payment.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch (UpvestProviderException ex)
        {
            // Unauthorised scope / unknown outcome / provider error: leave the payment PENDING to be reconciled later,
            // rather than reporting a failure for a write that may have landed.
            _logger.LogWarning("Payment {PaymentId}: not settled at Upvest ({Reason}); staying pending.",
                payment.PaymentId, ex.UnauthorizedScope ? "unauthorised scope" : (ex.OutcomeUnknown ? "unknown outcome" : $"status {(int?)ex.StatusCode}"));
            payment.Status = PaymentStatus.Pending;
            payment.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return new PaymentResult(payment.PaymentId, payment.Amount, ToWire(payment.Status));
    }

    public async Task<IReadOnlyList<PaymentResult>> ListPaymentsAsync(string shopperId, CancellationToken ct)
    {
        var payments = await _db.Payments
            .Where(p => p.ShopperId == shopperId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        foreach (var payment in payments.Where(p => p.Status == PaymentStatus.Pending && p.UpvestOrderId is not null))
        {
            try
            {
                var status = await _gateway.GetOrderStatusAsync(payment.UpvestOrderId!.Value, ct);
                if (status is not null)
                {
                    var mapped = MapOrderStatus(status);
                    if (mapped != payment.Status)
                    {
                        payment.Status = mapped;
                        payment.UpdatedAt = DateTimeOffset.UtcNow;
                    }
                }
            }
            catch (UpvestProviderException ex)
            {
                _logger.LogWarning("Payment {PaymentId}: status not refreshed ({Status}).", payment.PaymentId, (int?)ex.StatusCode);
            }
        }
        await _db.SaveChangesAsync(ct);

        return payments.Select(p => new PaymentResult(p.PaymentId, p.Amount, ToWire(p.Status))).ToList();
    }

    internal static PaymentStatus MapOrderStatus(string orderStatus) => orderStatus switch
    {
        "FILLED" or "SETTLED" => PaymentStatus.Invested,
        "CANCELLED" => PaymentStatus.Failed,
        _ => PaymentStatus.Pending,
    };

    internal static string ToWire(PaymentStatus status) => status switch
    {
        PaymentStatus.Invested => "invested",
        PaymentStatus.Failed => "failed",
        _ => "pending",
    };
}
