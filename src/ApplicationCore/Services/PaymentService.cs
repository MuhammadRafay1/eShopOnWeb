using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class PaymentService : IPaymentService
{
    // Namespaces invoice_id so it stays unique in PayPal's own (persistent, cross-restart) record
    // even though this app's in-memory store resets order ids back to 1 on every restart. In a
    // persisted-database deployment order ids are never reused, so this is purely a safety margin.
    private static readonly string RunId = Guid.NewGuid().ToString("N")[..8];

    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<CatalogItem> _catalogItemRepository;
    private readonly IRepository<SavedPaymentMethod> _savedPaymentMethodRepository;
    private readonly IUriComposer _uriComposer;
    private readonly IPayPalClient _payPalClient;
    private readonly PayPalSettings _settings;

    public PaymentService(
        IRepository<Order> orderRepository,
        IRepository<Payment> paymentRepository,
        IRepository<CatalogItem> catalogItemRepository,
        IRepository<SavedPaymentMethod> savedPaymentMethodRepository,
        IUriComposer uriComposer,
        IPayPalClient payPalClient,
        PayPalSettings settings)
    {
        _orderRepository = orderRepository;
        _paymentRepository = paymentRepository;
        _catalogItemRepository = catalogItemRepository;
        _savedPaymentMethodRepository = savedPaymentMethodRepository;
        _uriComposer = uriComposer;
        _payPalClient = payPalClient;
        _settings = settings;
    }

    public async Task<Order> CreateOrderAsync(string buyerId, IReadOnlyList<OrderLineRequest> lines, Address? shipToAddress)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));

        if (lines == null || lines.Count == 0)
        {
            throw new InvalidOrderRequestException("An order must contain at least one item.");
        }

        if (lines.Any(l => l.Quantity < 1))
        {
            throw new InvalidOrderRequestException("Every order line must have a quantity of at least 1.");
        }

        var catalogItemIds = lines.Select(l => l.CatalogItemId).Distinct().ToArray();
        var catalogItems = await _catalogItemRepository.ListAsync(new CatalogItemsSpecification(catalogItemIds));

        var missingIds = catalogItemIds.Except(catalogItems.Select(c => c.Id)).ToList();
        if (missingIds.Count > 0)
        {
            throw new InvalidOrderRequestException($"Unknown catalog item id(s): {string.Join(", ", missingIds)}");
        }

        var orderItems = lines.Select(line =>
        {
            var catalogItem = catalogItems.First(c => c.Id == line.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            return new OrderItem(itemOrdered, catalogItem.Price, line.Quantity);
        }).ToList();

        var address = shipToAddress ?? new Address("1 Unknown Street", "Redmond", "WA", "USA", "98052");

        var order = new Order(buyerId, address, orderItems);
        await _orderRepository.AddAsync(order);

        return order;
    }

    public Task<Payment> PayWithCardAsync(int orderId, string buyerId, CardDetails card) =>
        AuthorizeOrderAsync(orderId, buyerId, (amount, currency, customId, invoiceId, requestId) =>
            _payPalClient.AuthorizeOrderWithCardAsync(amount, currency, customId, invoiceId, card, requestId));

    public async Task<Payment> PayWithSavedCardAsync(int orderId, string buyerId, int savedPaymentMethodId)
    {
        var method = await _savedPaymentMethodRepository.GetByIdAsync(savedPaymentMethodId);
        if (method == null || method.OwnerId != buyerId)
        {
            throw new SavedPaymentMethodNotFoundException(savedPaymentMethodId);
        }

        return await AuthorizeOrderAsync(orderId, buyerId, (amount, currency, customId, invoiceId, requestId) =>
            _payPalClient.AuthorizeOrderWithVaultAsync(amount, currency, customId, invoiceId, method.VaultTokenId, requestId));
    }

    private async Task<Payment> AuthorizeOrderAsync(int orderId, string buyerId, Func<decimal, string, string, string, string, Task<AuthorizationResult>> authorize)
    {
        var order = await LoadOrderForBuyerAsync(orderId, buyerId);
        var existingPayment = await _paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpec(orderId));

        if (existingPayment?.AuthorizationId != null)
        {
            return existingPayment;
        }

        if (order.Status != OrderStatus.AwaitingPayment)
        {
            throw new InvalidOrderStateException($"Order {orderId} is not awaiting payment (status: {order.Status})");
        }

        var amount = order.Total();
        var currency = _settings.Currency;
        var customId = orderId.ToString(CultureInfo.InvariantCulture);
        var invoiceId = $"eshop-{RunId}-{orderId}";
        var requestId = $"pay-{orderId}";

        var result = await authorize(amount, currency, customId, invoiceId, requestId);

        if (result.RequiresChallenge)
        {
            throw new PaymentChallengeRequiredException(
                result.ChallengeReason ?? "PayPal requires an additional buyer-approval step (challenge/3DS) that this browserless integration does not support.");
        }

        var payment = existingPayment ?? new Payment(orderId, buyerId, currency, amount);
        payment.RecordAuthorization(result.PayPalOrderId, result.AuthorizationId, result.Status, result.ExpiresAt, result.CardBrand, result.CardLast4);

        if (existingPayment == null)
        {
            await _paymentRepository.AddAsync(payment);
        }
        else
        {
            await _paymentRepository.UpdateAsync(payment);
        }

        order.MarkAuthorized();
        await _orderRepository.UpdateAsync(order);

        return payment;
    }

    public async Task<Payment> FulfilAsync(int orderId)
    {
        var order = await LoadOrderAsync(orderId);
        var payment = await _paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpec(orderId));

        if (payment?.AuthorizationId == null)
        {
            throw new InvalidOrderStateException($"Order {orderId} has no authorized payment to fulfil");
        }

        if (payment.CaptureId != null)
        {
            return payment;
        }

        if (order.Status != OrderStatus.Authorized)
        {
            throw new InvalidOrderStateException($"Order {orderId} cannot be fulfilled from status {order.Status}");
        }

        var authorizationId = payment.AuthorizationId;
        var amount = payment.AuthorizedAmount;
        var currency = payment.Currency;

        if (payment.AuthorizationExpiresAt.HasValue && payment.AuthorizationExpiresAt.Value <= DateTimeOffset.UtcNow)
        {
            authorizationId = await RenewAuthorizationAsync(payment, amount, currency);
        }

        CaptureResult captureResult;
        try
        {
            captureResult = await _payPalClient.CaptureAsync(authorizationId, amount, currency, $"capture-{authorizationId}");
        }
        catch (PayPalApiException ex) when (ex.PayPalStatusCode == 422)
        {
            authorizationId = await RenewAuthorizationAsync(payment, amount, currency);
            captureResult = await _payPalClient.CaptureAsync(authorizationId, amount, currency, $"capture-{authorizationId}");
        }

        payment.RecordCapture(captureResult.CaptureId, captureResult.Status, captureResult.GrossAmount, captureResult.PayPalFee, captureResult.NetAmount);
        await _paymentRepository.UpdateAsync(payment);

        order.MarkFulfilled();
        await _orderRepository.UpdateAsync(order);

        return payment;
    }

    private async Task<string> RenewAuthorizationAsync(Payment payment, decimal amount, string currency)
    {
        try
        {
            var requestId = $"reauth-{payment.AuthorizationId}-{Guid.NewGuid():N}";
            var reauth = await _payPalClient.ReauthorizeAsync(payment.AuthorizationId!, amount, currency, requestId);
            payment.RenewAuthorization(reauth.AuthorizationId, reauth.Status, reauth.ExpiresAt);
            await _paymentRepository.UpdateAsync(payment);
            return reauth.AuthorizationId;
        }
        catch (PayPalApiException ex)
        {
            throw new AuthorizationNotRenewableException(
                $"The authorization for order {payment.OrderId} has gone stale and can no longer be renewed ({ex.Message}); collect payment again to fulfil this order.",
                ex.DebugId);
        }
    }

    public async Task<Payment> CancelAsync(int orderId)
    {
        var order = await LoadOrderAsync(orderId);
        var payment = await _paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpec(orderId));

        if (payment?.AuthorizationId == null)
        {
            throw new InvalidOrderStateException($"Order {orderId} has no authorized payment to cancel");
        }

        if (payment.Status == PaymentStatus.Voided)
        {
            return payment;
        }

        if (order.Status != OrderStatus.Authorized)
        {
            throw new InvalidOrderStateException($"Order {orderId} cannot be cancelled from status {order.Status}");
        }

        await _payPalClient.VoidAsync(payment.AuthorizationId, $"void-{payment.AuthorizationId}");

        payment.RecordVoid();
        await _paymentRepository.UpdateAsync(payment);

        order.MarkCancelled();
        await _orderRepository.UpdateAsync(order);

        return payment;
    }

    public async Task<(Payment Payment, Refund Refund)> RefundAsync(int orderId, string buyerId, decimal? amount, string idempotencyKey, string? note)
    {
        Guard.Against.NullOrEmpty(idempotencyKey, nameof(idempotencyKey));

        var order = await LoadOrderForBuyerAsync(orderId, buyerId);
        var payment = await _paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpec(orderId));

        if (payment?.CaptureId == null)
        {
            throw new InvalidOrderStateException($"Order {orderId} has not been captured; there is nothing to refund");
        }

        var existingRefund = payment.FindRefundByIdempotencyKey(idempotencyKey);
        if (existingRefund != null)
        {
            return (payment, existingRefund);
        }

        var remaining = payment.CapturedAmount!.Value - payment.TotalRefunded();
        if (amount.HasValue)
        {
            if (!payment.CanRefund(amount.Value))
            {
                throw new RefundExceedsCaptureException(
                    $"Refund of {amount.Value} {payment.Currency} would exceed the refundable balance ({remaining} {payment.Currency}) for order {orderId}");
            }
        }
        else if (remaining <= 0)
        {
            throw new RefundExceedsCaptureException($"Order {orderId} has already been refunded in full");
        }

        var result = await _payPalClient.RefundAsync(payment.CaptureId, amount, payment.Currency, idempotencyKey);

        var refund = payment.AddRefund(result.RefundId, result.Amount, result.Status, idempotencyKey, note);
        await _paymentRepository.UpdateAsync(payment);

        if (payment.Status == PaymentStatus.Refunded)
        {
            order.MarkRefunded();
        }
        else
        {
            order.MarkPartiallyRefunded();
        }
        await _orderRepository.UpdateAsync(order);

        return (payment, refund);
    }

    public async Task<IReadOnlyList<(Order Order, Payment? Payment)>> GetOrdersForBuyerAsync(string buyerId)
    {
        var orders = await _orderRepository.ListAsync(new CustomerOrdersWithItemsSpecification(buyerId));
        var orderIds = orders.Select(o => o.Id).ToList();
        var payments = await _paymentRepository.ListAsync(new PaymentsByOrderIdsSpec(orderIds));
        var paymentsByOrderId = payments.ToDictionary(p => p.OrderId);

        return orders
            .Select(o => (o, paymentsByOrderId.TryGetValue(o.Id, out var payment) ? payment : null))
            .ToList();
    }

    public async Task<SavedPaymentMethod> SavePaymentMethodAsync(string ownerId, CardDetails card)
    {
        Guard.Against.NullOrEmpty(ownerId, nameof(ownerId));

        var existingMethods = await _savedPaymentMethodRepository.ListAsync(new SavedPaymentMethodsByOwnerSpec(ownerId));
        var existingCustomerId = existingMethods.FirstOrDefault(m => m.PayPalCustomerId != null)?.PayPalCustomerId;

        var requestId = $"vault-{ownerId}-{Guid.NewGuid():N}";
        var result = await _payPalClient.VaultCardAsync(card, existingCustomerId, requestId);

        var method = new SavedPaymentMethod(
            ownerId,
            result.VaultTokenId,
            result.CustomerId ?? existingCustomerId,
            result.Brand,
            result.Last4,
            result.Expiry,
            card.Name);

        await _savedPaymentMethodRepository.AddAsync(method);

        return method;
    }

    public async Task<IReadOnlyList<SavedPaymentMethod>> GetPaymentMethodsAsync(string ownerId) =>
        await _savedPaymentMethodRepository.ListAsync(new SavedPaymentMethodsByOwnerSpec(ownerId));

    public async Task DeletePaymentMethodAsync(string ownerId, int paymentMethodId)
    {
        var method = await _savedPaymentMethodRepository.GetByIdAsync(paymentMethodId);
        if (method == null || method.OwnerId != ownerId)
        {
            throw new SavedPaymentMethodNotFoundException(paymentMethodId);
        }

        await _payPalClient.DeleteVaultTokenAsync(method.VaultTokenId);
        await _savedPaymentMethodRepository.DeleteAsync(method);
    }

    public async Task<ReconciliationReport> ReconcileAsync(DateTimeOffset from, DateTimeOffset to)
    {
        var transactions = await _payPalClient.SearchTransactionsAsync(from, to, _settings.Currency);

        var orders = await _orderRepository.ListAsync(new OrdersByDateRangeSpec(from, to));
        var orderIds = orders.Select(o => o.Id).ToList();
        var payments = (await _paymentRepository.ListAsync(new PaymentsByOrderIdsSpec(orderIds)))
            .Where(p => p.AuthorizationId != null)
            .ToDictionary(p => p.OrderId);

        // Match on the *exact* invoice_id we ourselves generated ("eshop-{RunId}-{orderId}") for each
        // known payment, rather than parsing an arbitrary transaction's invoice_id and trusting the
        // trailing number. On a long-lived, reused sandbox account, transaction search returns years of
        // unrelated history; parsing alone lets an old run's "eshop-{otherRunId}-2" collide with this
        // run's order 2 just because both end in "-2". An exact-string map cannot collide that way.
        var invoiceIdToOrderId = payments.Values.ToDictionary(p => $"eshop-{RunId}-{p.OrderId}", p => p.OrderId);

        var matched = new List<ReconciliationEntry>();
        var payPalOnly = new List<ReconciliationEntry>();
        var matchedOrderIds = new HashSet<int>();

        foreach (var txn in transactions)
        {
            if (txn.InvoiceId != null && invoiceIdToOrderId.TryGetValue(txn.InvoiceId, out var orderId) && payments.TryGetValue(orderId, out var payment))
            {
                matched.Add(new ReconciliationEntry(
                    orderId, txn.TransactionId, txn.InvoiceId, txn.Amount, txn.FeeAmount, txn.Status,
                    payment.CapturedAmount, payment.Status.ToString()));
                matchedOrderIds.Add(orderId);
            }
            else
            {
                // Best-effort order id for display only; never used to decide matched vs. PayPal-only.
                var looksLikeOurs = TryParseOrderId(txn.InvoiceId);
                payPalOnly.Add(new ReconciliationEntry(
                    looksLikeOurs, txn.TransactionId, txn.InvoiceId, txn.Amount, txn.FeeAmount, txn.Status, null, null));
            }
        }

        var eShopOnly = payments.Values
            .Where(p => !matchedOrderIds.Contains(p.OrderId))
            .Select(p => new ReconciliationEntry(
                p.OrderId, null, $"eshop-{RunId}-{p.OrderId}", null, null, null,
                p.CapturedAmount ?? p.AuthorizedAmount, p.Status.ToString()))
            .ToList();

        return new ReconciliationReport(from, to, matched, payPalOnly, eShopOnly);
    }

    private const string InvoiceIdPrefix = "eshop-";

    /// <summary>Parses the order id out of one of our own invoice_id values ("eshop-{runId}-{orderId}").
    /// Deliberately only recognises this exact, namespaced shape -- an unrelated invoice_id that merely
    /// ends in "-123" must not be mistaken for one of ours.</summary>
    private static int? TryParseOrderId(string? invoiceId)
    {
        if (string.IsNullOrEmpty(invoiceId) || !invoiceId.StartsWith(InvoiceIdPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var lastDash = invoiceId.LastIndexOf('-');
        if (lastDash < InvoiceIdPrefix.Length - 1 || lastDash == invoiceId.Length - 1)
        {
            return null;
        }

        var candidate = invoiceId[(lastDash + 1)..];
        return int.TryParse(candidate, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;
    }

    private async Task<Order> LoadOrderForBuyerAsync(int orderId, string buyerId)
    {
        var order = await _orderRepository.FirstOrDefaultAsync(new OrderWithItemsByIdSpec(orderId));
        if (order == null || order.BuyerId != buyerId)
        {
            throw new OrderNotFoundException(orderId);
        }
        return order;
    }

    private async Task<Order> LoadOrderAsync(int orderId)
    {
        var order = await _orderRepository.FirstOrDefaultAsync(new OrderWithItemsByIdSpec(orderId));
        if (order == null)
        {
            throw new OrderNotFoundException(orderId);
        }
        return order;
    }
}
