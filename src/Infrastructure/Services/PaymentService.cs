using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Services;

/// <summary>
/// End-to-end orchestration of the payment flows. Each method takes a claim in the local store before
/// calling PayPal (so a double-click cannot act twice), carries the idempotency keys, and settles
/// unknown outcomes rather than reporting a bare failure. Lives in Infrastructure because the claim/retry
/// logic depends on EF Core's concurrency and unique-index exceptions.
/// </summary>
public sealed class PaymentService : IPaymentService
{
    private readonly IRepository<Order> _orders;
    private readonly IRepository<PaymentMethod> _paymentMethods;
    private readonly IRepository<CatalogItem> _catalogItems;
    private readonly IPayPalGateway _gateway;
    private readonly IUriComposer _uriComposer;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IRepository<Order> orders,
        IRepository<PaymentMethod> paymentMethods,
        IRepository<CatalogItem> catalogItems,
        IPayPalGateway gateway,
        IUriComposer uriComposer,
        ILogger<PaymentService> logger)
    {
        _orders = orders;
        _paymentMethods = paymentMethods;
        _catalogItems = catalogItems;
        _gateway = gateway;
        _uriComposer = uriComposer;
        _logger = logger;
    }

    // ---------------------------------------------------------------- Place order

    public async Task<int> PlaceOrderAsync(string buyerId, IReadOnlyList<OrderLine> lines, ShipTo shipTo, CancellationToken ct)
    {
        if (lines is null || lines.Count == 0)
            throw new ArgumentException("An order must contain at least one line item.", nameof(lines));
        if (lines.Any(l => l.Quantity <= 0))
            throw new ArgumentException("Every order line must have a positive quantity.", nameof(lines));

        var ids = lines.Select(l => l.CatalogItemId).Distinct().ToArray();
        var catalogItems = await _catalogItems.ListAsync(new CatalogItemsSpecification(ids), ct);
        var byId = catalogItems.ToDictionary(c => c.Id);

        var missing = ids.Where(id => !byId.ContainsKey(id)).ToArray();
        if (missing.Length > 0)
            throw new ArgumentException($"Unknown catalog item id(s): {string.Join(", ", missing)}.", nameof(lines));

        var items = lines.Select(line =>
        {
            var catalogItem = byId[line.CatalogItemId];
            var ordered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            return new OrderItem(ordered, catalogItem.Price, line.Quantity);
        }).ToList();

        var address = new Address(shipTo.Street, shipTo.City, shipTo.State, shipTo.Country, shipTo.ZipCode);
        var order = new Order(buyerId, address, items);
        order.InitializePayment(_gateway.Currency, NewInvoiceReference());

        var saved = await _orders.AddAsync(order, ct);
        return saved.Id;
    }

    // ---------------------------------------------------------------- Pay (authorize)

    public async Task<OrderPaymentView> PayAsync(string buyerId, int orderId, PayCommand command, CancellationToken ct)
    {
        if (command.Card is null && command.PaymentMethodId is null)
            throw new ArgumentException("Provide either card details or a saved paymentMethodId.");
        if (command.Card is not null && command.PaymentMethodId is not null)
            throw new ArgumentException("Provide either card details or a saved paymentMethodId, not both.");

        var order = await _orders.FirstOrDefaultAsync(new OrderByIdForBuyerSpecification(orderId, buyerId), ct);
        if (order?.Payment is null)
            throw new OrderNotFoundException(orderId);

        string? vaultId = null;
        if (command.PaymentMethodId is int pmId)
        {
            var pm = await _paymentMethods.FirstOrDefaultAsync(new PaymentMethodByIdForBuyerSpecification(pmId, buyerId), ct);
            if (pm is null)
                throw new PaymentMethodNotFoundException(pmId);
            vaultId = pm.PayPalVaultId;
        }

        // Claim: AwaitingPayment -> Authorizing, persisted before the PayPal call.
        try
        {
            order.BeginAuthorizing();
            await _orders.UpdateAsync(order, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await ReloadBuyerViewAsync(orderId, buyerId, ct);
        }

        var payment = order.Payment!;
        // Idempotency keys are derived from the globally-unique invoice reference (a per-order GUID), never
        // the order id — the in-memory store resets order ids to small integers each run, so an id-based key
        // would collide with a prior run and PayPal (which retains keys for hours/days) would return a stale
        // result for an unrelated order.
        var authorizeCommand = new AuthorizeCardPaymentCommand
        {
            Amount = order.Total(),
            InvoiceId = payment.InvoiceReference,
            IdempotencyKey = $"pay-{payment.InvoiceReference}-{payment.Version}",
            Description = $"eShopOnWeb order {orderId}",
            Card = command.Card,
            VaultId = vaultId
        };

        try
        {
            var result = await _gateway.AuthorizeAsync(authorizeCommand, ct);
            order.MarkAuthorized(result.PayPalOrderId, result.AuthorizationId, result.AuthorizationStatus, result.AuthorizedAmount, result.ExpiresAt);
            await _orders.UpdateAsync(order, ct);
            return MapOrder(order);
        }
        catch (PaymentOutcomeUnknownException)
        {
            order.MarkUnknown();
            await _orders.UpdateAsync(order, ct);
            throw;
        }
        catch (PaymentGatewayException)
        {
            // Declined / payer-action / provider error: no hold was placed — let the shopper retry.
            order.FailAuthorization();
            await _orders.UpdateAsync(order, ct);
            throw;
        }
    }

    // ---------------------------------------------------------------- Fulfil (capture)

    public async Task<OrderPaymentView> FulfilAsync(int orderId, CancellationToken ct)
    {
        var order = await _orders.FirstOrDefaultAsync(new OrderWithPaymentByIdSpecification(orderId), ct);
        if (order?.Payment is null)
            throw new OrderNotFoundException(orderId);

        try
        {
            order.BeginCapturing();
            await _orders.UpdateAsync(order, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await ReloadOperatorViewAsync(orderId, ct);
        }

        var payment = order.Payment!;
        var authorizationId = payment.PayPalAuthorizationId
            ?? throw new InvalidOrderStateException("Order has no authorization to capture.");

        try
        {
            // Renew a stale hold rather than letting the capture fail outright.
            var auth = await _gateway.GetAuthorizationAsync(authorizationId, ct);
            if (IsStale(auth))
            {
                _logger.LogInformation("Authorization {AuthId} for order {OrderId} is stale ({Status}, expires {Expiry}); reauthorizing.",
                    authorizationId, orderId, auth.Status, auth.ExpiresAt);
                var reauth = await _gateway.ReauthorizeAsync(authorizationId, order.Total(), $"reauth-{payment.InvoiceReference}-{payment.Version}", ct);
                order.ApplyReauthorization(reauth.AuthorizationId, reauth.Status, null, reauth.ExpiresAt);
                await _orders.UpdateAsync(order, ct);
                authorizationId = reauth.AuthorizationId;
            }

            var capture = await _gateway.CaptureAsync(authorizationId, order.Total(), $"fulfil-{payment.InvoiceReference}", ct);
            order.MarkFulfilled(capture.CaptureId, capture.Status, capture.GrossAmount, capture.PayPalFee, capture.NetAmount);
            await _orders.UpdateAsync(order, ct);
            return MapOrder(order);
        }
        catch (PaymentOutcomeUnknownException)
        {
            order.MarkUnknown();
            await _orders.UpdateAsync(order, ct);
            throw;
        }
        catch (PaymentGatewayException)
        {
            // Includes the "can no longer be renewed" case — the message carries PayPal's debug id.
            order.FailCapture();
            await _orders.UpdateAsync(order, ct);
            throw;
        }
    }

    // ---------------------------------------------------------------- Cancel (void)

    public async Task<OrderPaymentView> CancelAsync(int orderId, CancellationToken ct)
    {
        var order = await _orders.FirstOrDefaultAsync(new OrderWithPaymentByIdSpecification(orderId), ct);
        if (order?.Payment is null)
            throw new OrderNotFoundException(orderId);

        var wasAuthorized = order.Status == OrderStatus.Authorized;

        try
        {
            order.BeginCancelling();
            await _orders.UpdateAsync(order, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await ReloadOperatorViewAsync(orderId, ct);
        }

        try
        {
            if (wasAuthorized && order.Payment!.PayPalAuthorizationId is { } authId)
            {
                var result = await _gateway.VoidAsync(authId, $"cancel-{order.Payment!.InvoiceReference}", ct);
                order.MarkCancelled(result.Status);
            }
            else
            {
                // Never authorized — nothing to release at PayPal.
                order.MarkCancelled();
            }
            await _orders.UpdateAsync(order, ct);
            return MapOrder(order);
        }
        catch (PaymentOutcomeUnknownException)
        {
            order.MarkUnknown();
            await _orders.UpdateAsync(order, ct);
            throw;
        }
        catch (PaymentGatewayException)
        {
            order.FailCancellation();
            await _orders.UpdateAsync(order, ct);
            throw;
        }
    }

    // ---------------------------------------------------------------- Refund

    public async Task<RefundResultView> RefundAsync(int orderId, decimal? amount, string idempotencyKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("A refund idempotency key is required.", nameof(idempotencyKey));

        var order = await _orders.FirstOrDefaultAsync(new OrderWithPaymentByIdSpecification(orderId), ct);
        if (order?.Payment is null)
            throw new OrderNotFoundException(orderId);

        var payment = order.Payment!;
        var captureId = payment.PayPalCaptureId
            ?? throw new InvalidOrderStateException("Order has no captured payment to refund.");

        var prefixedKey = $"refund-{payment.InvoiceReference}-{idempotencyKey}";

        // Idempotent replay: the same key returns the earlier refund rather than issuing a second one.
        var existing = payment.Refunds.FirstOrDefault(r => r.IdempotencyKey == prefixedKey);
        if (existing is not null)
            return new RefundResultView { RefundId = existing.Id, Order = MapOrder(order) };

        var remaining = payment.RefundableRemaining();
        var requested = amount ?? remaining;
        if (requested <= 0m || requested > remaining)
            throw new OverRefundException(requested, remaining);

        // Claim: insert the refund row (PENDING) before calling PayPal. Unique index on
        // (OrderPaymentId, IdempotencyKey) rejects a concurrent duplicate; PayPal's own idempotency key
        // is the ultimate guard that a resend never refunds twice.
        var refund = new OrderRefund(prefixedKey, requested, _gateway.Currency);
        order.AttachRefund(refund);
        try
        {
            await _orders.UpdateAsync(order, ct);
        }
        catch (DbUpdateException)
        {
            var reloaded = await _orders.FirstOrDefaultAsync(new OrderWithPaymentByIdSpecification(orderId), ct);
            var prior = reloaded?.Payment?.Refunds.FirstOrDefault(r => r.IdempotencyKey == prefixedKey);
            if (prior is not null && reloaded is not null)
                return new RefundResultView { RefundId = prior.Id, Order = MapOrder(reloaded) };
            throw;
        }

        try
        {
            var result = await _gateway.RefundAsync(captureId, requested, prefixedKey, ct);
            refund.MarkSucceeded(result.RefundId, result.Status ?? "COMPLETED");
            var fullyRefunded = payment.RefundedAmount() >= (payment.CapturedAmount ?? 0m);
            order.MarkRefunded(fullyRefunded);
            await _orders.UpdateAsync(order, ct);
            return new RefundResultView { RefundId = refund.Id, Order = MapOrder(order) };
        }
        catch (PaymentOutcomeUnknownException)
        {
            // Leave the refund PENDING (it still counts against the capture, preventing over-refund) and
            // park the order as Unknown for a sweep to settle.
            order.MarkUnknown();
            await _orders.UpdateAsync(order, ct);
            throw;
        }
        catch (PaymentGatewayException)
        {
            refund.MarkFailed();
            await _orders.UpdateAsync(order, ct);
            throw;
        }
    }

    // ---------------------------------------------------------------- Queries

    public async Task<IReadOnlyList<OrderPaymentView>> GetMyOrdersAsync(string buyerId, CancellationToken ct)
    {
        var orders = await _orders.ListAsync(new CustomerOrdersWithPaymentSpecification(buyerId), ct);
        return orders.Select(MapOrder).ToList();
    }

    public async Task<ReconciliationView> ReconcileAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var report = await _gateway.SearchTransactionsAsync(from, to, ct);
        var orders = await _orders.ListAsync(new OrdersWithPaymentSpecification(), ct);

        // Index eShop orders by every PayPal id/reference they carry.
        var byPayPalId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var o in orders)
        {
            var p = o.Payment!;
            foreach (var key in new[] { p.PayPalCaptureId, p.PayPalAuthorizationId, p.PayPalOrderId, p.InvoiceReference })
                if (!string.IsNullOrEmpty(key))
                    byPayPalId.TryAdd(key!, o.Id);
        }

        var matchedOrderIds = new HashSet<int>();
        var entries = report.Transactions.Select(t =>
        {
            int? matched = null;
            foreach (var key in new[] { t.TransactionId, t.ReferenceId, t.InvoiceId })
            {
                if (!string.IsNullOrEmpty(key) && byPayPalId.TryGetValue(key!, out var oid))
                {
                    matched = oid;
                    matchedOrderIds.Add(oid);
                    break;
                }
            }
            return new ReconciliationEntry
            {
                TransactionId = t.TransactionId,
                ReferenceId = t.ReferenceId,
                Amount = t.Amount,
                Currency = t.Currency,
                Status = t.Status,
                InvoiceId = t.InvoiceId,
                InitiatedAt = t.InitiatedAt,
                MatchedOrderId = matched
            };
        }).ToList();

        var missing = orders
            .Where(o => o.Payment!.PayPalCaptureId is not null)
            .Where(o => o.OrderDate >= from && o.OrderDate <= to)
            .Where(o => !matchedOrderIds.Contains(o.Id))
            .Select(o => o.Id)
            .ToList();

        return new ReconciliationView
        {
            From = from,
            To = to,
            Truncated = report.Truncated,
            PayPalTransactions = entries,
            EShopOrdersMissingFromPayPal = missing
        };
    }

    // ---------------------------------------------------------------- Saved cards

    public async Task<SavedCardView> SaveCardAsync(string buyerId, CardDetails card, CancellationToken ct)
    {
        // Reuse this shopper's existing PayPal customer id so all their cards land under one customer.
        var existing = await _paymentMethods.ListAsync(new PaymentMethodsForBuyerSpecification(buyerId), ct);
        var existingCustomerId = existing.FirstOrDefault()?.PayPalCustomerId;

        var result = await _gateway.SaveCardAsync(new SaveCardCommand
        {
            MerchantCustomerId = MerchantCustomerId(buyerId),
            ExistingPayPalCustomerId = existingCustomerId,
            Card = card
        }, ct);

        var method = new PaymentMethod(buyerId, result.VaultId, result.PayPalCustomerId, result.Brand, result.LastDigits, result.Expiry);
        var saved = await _paymentMethods.AddAsync(method, ct);
        return MapCard(saved);
    }

    public async Task<IReadOnlyList<SavedCardView>> GetSavedCardsAsync(string buyerId, CancellationToken ct)
    {
        var methods = await _paymentMethods.ListAsync(new PaymentMethodsForBuyerSpecification(buyerId), ct);
        return methods.Select(MapCard).ToList();
    }

    public async Task DeleteSavedCardAsync(string buyerId, int paymentMethodId, CancellationToken ct)
    {
        var method = await _paymentMethods.FirstOrDefaultAsync(new PaymentMethodByIdForBuyerSpecification(paymentMethodId, buyerId), ct);
        if (method is null)
            throw new PaymentMethodNotFoundException(paymentMethodId);

        // Remove at PayPal first so a deleted card can never be used to pay; then drop the local row.
        await _gateway.DeleteSavedCardAsync(method.PayPalVaultId, ct);
        await _paymentMethods.DeleteAsync(method, ct);
    }

    // ---------------------------------------------------------------- Helpers

    private static bool IsStale(PayPalAuthorizationState auth)
    {
        if (auth.ExpiresAt is { } expiry && expiry <= DateTimeOffset.UtcNow)
            return true;
        // Any status other than a fresh, capturable hold is treated as needing renewal.
        return auth.Status is not null
            && !string.Equals(auth.Status, "CREATED", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(auth.Status, "PENDING", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<OrderPaymentView> ReloadBuyerViewAsync(int orderId, string buyerId, CancellationToken ct)
    {
        var order = await _orders.FirstOrDefaultAsync(new OrderByIdForBuyerSpecification(orderId, buyerId), ct)
            ?? throw new OrderNotFoundException(orderId);
        return MapOrder(order);
    }

    private async Task<OrderPaymentView> ReloadOperatorViewAsync(int orderId, CancellationToken ct)
    {
        var order = await _orders.FirstOrDefaultAsync(new OrderWithPaymentByIdSpecification(orderId), ct)
            ?? throw new OrderNotFoundException(orderId);
        return MapOrder(order);
    }

    private static string NewInvoiceReference() => $"ESHOP-{Guid.NewGuid():N}";

    /// <summary>
    /// A stable, PayPal-pattern-safe id for a shopper. PayPal's <c>merchant_customer_id</c> allows
    /// <c>[0-9a-zA-Z-_.^*$@#]</c>; the eShop buyer id (an email/username) already fits, truncated to 64.
    /// </summary>
    private static string MerchantCustomerId(string buyerId) =>
        buyerId.Length <= 64 ? buyerId : buyerId[..64];

    private static OrderPaymentView MapOrder(Order order)
    {
        var p = order.Payment;
        return new OrderPaymentView
        {
            OrderId = order.Id,
            Status = order.Status.ToString(),
            Total = order.Total(),
            Currency = p?.Currency ?? string.Empty,
            PayPalOrderId = p?.PayPalOrderId,
            AuthorizationId = p?.PayPalAuthorizationId,
            AuthorizationStatus = p?.AuthorizationStatus,
            AuthorizedAmount = p?.AuthorizedAmount,
            AuthorizationExpiresAt = p?.AuthorizationExpiresAt,
            CaptureId = p?.PayPalCaptureId,
            CaptureStatus = p?.CaptureStatus,
            CapturedAmount = p?.CapturedAmount,
            PayPalFee = p?.PayPalFeeAmount,
            NetProceeds = p?.NetAmount,
            RefundedAmount = p?.RefundedAmount() ?? 0m,
            Refunds = p?.Refunds.Select(r => new RefundView
            {
                RefundId = r.Id,
                PayPalRefundId = r.PayPalRefundId,
                Amount = r.Amount,
                Status = r.Status,
                CreatedAt = r.CreatedAt
            }).ToList() ?? new List<RefundView>()
        };
    }

    private static SavedCardView MapCard(PaymentMethod method) => new()
    {
        PaymentMethodId = method.Id,
        Brand = method.Brand,
        LastDigits = method.LastDigits,
        Expiry = method.Expiry,
        CreatedAt = method.CreatedAt
    };
}
