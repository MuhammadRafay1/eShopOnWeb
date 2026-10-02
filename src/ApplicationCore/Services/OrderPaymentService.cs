using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class OrderPaymentService : IOrderPaymentService
{
    private static readonly HashSet<string> ExpiredAuthIssues = new(StringComparer.OrdinalIgnoreCase)
    {
        "AUTHORIZATION_EXPIRED", "AUTH_EXPIRED", "INVALID_AUTHORIZATION_ID"
    };

    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<CatalogItem> _catalogRepository;
    private readonly IRepository<OrderPayment> _paymentRepository;
    private readonly IRepository<SavedCard> _savedCardRepository;
    private readonly IPaymentGateway _gateway;
    private readonly IPaymentLock _lock;
    private readonly IAppLogger<OrderPaymentService> _logger;

    public OrderPaymentService(
        IRepository<Order> orderRepository,
        IRepository<CatalogItem> catalogRepository,
        IRepository<OrderPayment> paymentRepository,
        IRepository<SavedCard> savedCardRepository,
        IPaymentGateway gateway,
        IPaymentLock paymentLock,
        IAppLogger<OrderPaymentService> logger)
    {
        _orderRepository = orderRepository;
        _catalogRepository = catalogRepository;
        _paymentRepository = paymentRepository;
        _savedCardRepository = savedCardRepository;
        _gateway = gateway;
        _lock = paymentLock;
        _logger = logger;
    }

    private static string OrderGate(int orderId) => $"order:{orderId}";

    public async Task<int> PlaceOrderAsync(string buyerId, IReadOnlyList<OrderLineInput> items, ShippingAddressInput? address, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        if (items is null || items.Count == 0)
        {
            throw new PaymentValidationException("At least one order item is required.");
        }

        foreach (var line in items)
        {
            if (line.Quantity <= 0)
            {
                throw new PaymentValidationException($"Quantity for catalog item {line.CatalogItemId} must be greater than zero.");
            }
        }

        var ids = items.Select(i => i.CatalogItemId).Distinct().ToArray();
        var catalogItems = await _catalogRepository.ListAsync(new CatalogItemsSpecification(ids), cancellationToken);
        var catalogById = catalogItems.ToDictionary(c => c.Id);

        var missing = ids.Where(id => !catalogById.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            throw new PaymentValidationException($"Unknown catalog item id(s): {string.Join(", ", missing)}.");
        }

        var orderItems = new List<OrderItem>();
        foreach (var line in items)
        {
            var catalog = catalogById[line.CatalogItemId];
            var pictureUri = string.IsNullOrEmpty(catalog.PictureUri) ? "eCatalog-item-default.png" : catalog.PictureUri;
            var itemOrdered = new CatalogItemOrdered(catalog.Id, catalog.Name, pictureUri);
            orderItems.Add(new OrderItem(itemOrdered, catalog.Price, line.Quantity));
        }

        var shipToAddress = BuildAddress(address);
        var order = new Order(buyerId, shipToAddress, orderItems);
        order = await _orderRepository.AddAsync(order, cancellationToken);

        var total = order.Total();
        var invoiceId = $"ESHOP-{order.Id}-{Guid.NewGuid():N}"; // unique per order, used for reconciliation
        var payment = new OrderPayment(order.Id, buyerId, _gateway.Currency, total, invoiceId);
        await _paymentRepository.AddAsync(payment, cancellationToken);

        _logger.LogInformation($"Placed order {order.Id} for {buyerId}: total {total} {_gateway.Currency}, invoice {invoiceId}.");
        return order.Id;
    }

    public async Task<OrderPaymentView> PayAsync(string buyerId, int orderId, PayInput input, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        using var gate = await _lock.AcquireAsync(OrderGate(orderId), cancellationToken);

        var payment = await LoadOwnedPaymentAsync(orderId, buyerId, cancellationToken);

        // Idempotent: a double-click never authorizes twice.
        if (payment.Status == PaymentStatus.Authorized)
        {
            return await BuildViewAsync(payment, cancellationToken);
        }
        if (payment.Status is PaymentStatus.Captured or PaymentStatus.PartiallyRefunded or PaymentStatus.Refunded)
        {
            return await BuildViewAsync(payment, cancellationToken);
        }
        if (payment.Status is not (PaymentStatus.AwaitingPayment or PaymentStatus.Failed))
        {
            throw new PaymentConflictException($"Order {orderId} is {payment.Status} and cannot be paid.");
        }

        var (card, vaultId) = await ResolvePaymentSourceAsync(buyerId, input, cancellationToken);

        var request = new AuthorizeGatewayRequest
        {
            OrderId = orderId,
            InvoiceId = payment.InvoiceId,
            Amount = payment.Amount,
            Description = $"eShopOnWeb order {orderId}",
            Card = card,
            SavedCardVaultId = vaultId,
            CreateIdempotencyKey = payment.CreateOrderIdempotencyKey,
            AuthorizeIdempotencyKey = payment.AuthorizeIdempotencyKey
        };

        try
        {
            var auth = await _gateway.AuthorizeAsync(request, cancellationToken);
            payment.MarkAuthorized(auth.PayPalOrderId, auth.AuthorizationId, auth.Status, auth.ExpiresAt);
            await _paymentRepository.UpdateAsync(payment, cancellationToken);
            _logger.LogInformation($"Authorized order {orderId}: authorization {auth.AuthorizationId} ({auth.Status}).");
            return await BuildViewAsync(payment, cancellationToken);
        }
        catch (PaymentChallengeRequiredException)
        {
            throw;
        }
        catch (PaymentGatewayException ex) when (ex.IsCallerError && !ex.OutcomeUnknown)
        {
            // Definitive decline — money was not held. Rotate keys so a retry with another card is fresh.
            payment.MarkDeclined(ex.Message);
            await _paymentRepository.UpdateAsync(payment, cancellationToken);
            _logger.LogWarning($"Authorization declined for order {orderId}: {ex.Message} (debug_id {ex.PayPalDebugId}).");
            throw new PaymentValidationException($"The payment was declined: {ex.Message}");
        }
        // Non-caller / unknown-outcome gateway errors: leave status unchanged (money may be held) and
        // let the error propagate; the stable idempotency keys make a retry safe.
    }

    public async Task<OrderPaymentView> FulfilAsync(int orderId, CancellationToken cancellationToken = default)
    {
        using var gate = await _lock.AcquireAsync(OrderGate(orderId), cancellationToken);
        var payment = await LoadPaymentAsync(orderId, cancellationToken);

        if (payment.Status is PaymentStatus.Captured or PaymentStatus.PartiallyRefunded or PaymentStatus.Refunded)
        {
            return await BuildViewAsync(payment, cancellationToken); // already captured — idempotent
        }
        if (payment.Status != PaymentStatus.Authorized || payment.AuthorizationId is null)
        {
            throw new PaymentConflictException($"Order {orderId} is {payment.Status} and cannot be fulfilled; it must be authorized first.");
        }

        var authorizationId = payment.AuthorizationId;

        // Proactively renew a hold that has gone (or is about to go) stale before fulfilment.
        var now = DateTimeOffset.UtcNow;
        if (payment.AuthorizationExpiresAt is { } expiry && expiry <= now.AddMinutes(5))
        {
            authorizationId = await RenewAuthorizationOrThrowAsync(payment, cancellationToken);
        }

        try
        {
            return await CaptureAsync(payment, authorizationId, cancellationToken);
        }
        catch (PaymentGatewayException ex) when (IsExpiredAuthorization(ex))
        {
            // The hold went stale between our check and the capture: renew, then capture again.
            _logger.LogWarning($"Capture of order {orderId} reported a stale authorization ({ex.PayPalIssue}); renewing.");
            authorizationId = await RenewAuthorizationOrThrowAsync(payment, cancellationToken);
            return await CaptureAsync(payment, authorizationId, cancellationToken);
        }
    }

    public async Task<OrderPaymentView> CancelAsync(int orderId, CancellationToken cancellationToken = default)
    {
        using var gate = await _lock.AcquireAsync(OrderGate(orderId), cancellationToken);
        var payment = await LoadPaymentAsync(orderId, cancellationToken);

        if (payment.Status == PaymentStatus.Cancelled)
        {
            return await BuildViewAsync(payment, cancellationToken); // idempotent
        }
        if (payment.Status != PaymentStatus.Authorized || payment.AuthorizationId is null)
        {
            throw new PaymentConflictException($"Order {orderId} is {payment.Status} and cannot be cancelled; only an authorized, un-captured order can be.");
        }

        await _gateway.VoidAsync(payment.AuthorizationId, payment.VoidIdempotencyKey, cancellationToken);
        payment.MarkCancelled();
        await _paymentRepository.UpdateAsync(payment, cancellationToken);
        _logger.LogInformation($"Cancelled order {orderId}: authorization {payment.AuthorizationId} voided.");
        return await BuildViewAsync(payment, cancellationToken);
    }

    public async Task<RefundLineView> RefundAsync(string buyerId, int orderId, RefundInput input, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        if (string.IsNullOrWhiteSpace(input.IdempotencyKey))
        {
            throw new PaymentValidationException("An idempotency key is required for refunds.");
        }

        using var gate = await _lock.AcquireAsync(OrderGate(orderId), cancellationToken);
        var payment = await LoadOwnedPaymentAsync(orderId, buyerId, cancellationToken);

        if (payment.Status is not (PaymentStatus.Captured or PaymentStatus.PartiallyRefunded) || payment.CaptureId is null)
        {
            throw new PaymentConflictException($"Order {orderId} is {payment.Status}; only a captured order can be refunded.");
        }

        decimal claimAmount;
        decimal? gatewayAmount;
        PaymentRefund claim;

        var existing = payment.FindRefundByKey(input.IdempotencyKey);
        if (existing is not null)
        {
            if (existing.PayPalRefundId is not null)
            {
                return ToRefundLine(existing); // already refunded under this key — do not refund again
            }
            // A prior attempt did not settle (PENDING/UNKNOWN): replay with the same key; PayPal dedupes.
            claim = existing;
            claimAmount = existing.Amount;
            gatewayAmount = existing.Amount;
        }
        else
        {
            var remaining = payment.RefundableRemaining();
            claimAmount = input.Amount ?? remaining;
            if (claimAmount <= 0m)
            {
                throw new PaymentValidationException("There is nothing left to refund on this order.");
            }
            if (claimAmount > remaining)
            {
                throw new PaymentValidationException($"A refund of {claimAmount:0.00} exceeds the remaining refundable amount of {remaining:0.00} {payment.CurrencyCode}.");
            }
            // Claim first (a write of our own), under the lock, before the SDK call.
            claim = payment.AddRefundClaim(input.IdempotencyKey, claimAmount);
            await _paymentRepository.UpdateAsync(payment, cancellationToken);
            gatewayAmount = input.Amount; // null => refund the full remaining amount
        }

        try
        {
            var result = await _gateway.RefundAsync(payment.CaptureId, gatewayAmount, input.IdempotencyKey, payment.InvoiceId, cancellationToken);
            claim.Settle(result.RefundId, result.Status ?? "COMPLETED");
            payment.RecalculateRefundState();
            await _paymentRepository.UpdateAsync(payment, cancellationToken);
            _logger.LogInformation($"Refunded {claimAmount:0.00} {payment.CurrencyCode} on order {orderId}: refund {result.RefundId} ({result.Status}).");
            return ToRefundLine(claim);
        }
        catch (PaymentGatewayException ex) when (ex.OutcomeUnknown)
        {
            claim.MarkUnknown();
            await _paymentRepository.UpdateAsync(payment, cancellationToken);
            _logger.LogWarning($"Refund outcome unknown for order {orderId} (key {input.IdempotencyKey}); retry with the same key to settle it.");
            throw;
        }
        catch (PaymentGatewayException ex) when (ex.IsCallerError)
        {
            claim.MarkFailed();
            payment.RecalculateRefundState();
            await _paymentRepository.UpdateAsync(payment, cancellationToken);
            throw new PaymentValidationException($"Refund rejected: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<OrderPaymentView>> GetMyOrdersAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        var payments = await _paymentRepository.ListAsync(new OrderPaymentsByBuyerSpecification(buyerId), cancellationToken);
        var orders = await _orderRepository.ListAsync(new CustomerOrdersWithItemsSpecification(buyerId), cancellationToken);
        var ordersById = orders.ToDictionary(o => o.Id);

        var views = new List<OrderPaymentView>();
        foreach (var payment in payments)
        {
            ordersById.TryGetValue(payment.OrderId, out var order);
            views.Add(OrderPaymentView.From(payment, order?.OrderDate ?? payment.CreatedAt, BuildItems(order)));
        }
        return views.OrderByDescending(v => v.OrderDate).ToList();
    }

    // --- helpers -------------------------------------------------------------------------------

    private async Task<OrderPaymentView> CaptureAsync(OrderPayment payment, string authorizationId, CancellationToken cancellationToken)
    {
        var capture = await _gateway.CaptureAsync(authorizationId, payment.CaptureIdempotencyKey, cancellationToken);
        payment.MarkCaptured(capture.CaptureId, capture.Status, capture.GrossAmount, capture.PayPalFee, capture.NetAmount);
        await _paymentRepository.UpdateAsync(payment, cancellationToken);
        _logger.LogInformation($"Captured order {payment.OrderId}: capture {capture.CaptureId} gross {capture.GrossAmount} fee {capture.PayPalFee} net {capture.NetAmount}.");
        return await BuildViewAsync(payment, cancellationToken);
    }

    private async Task<string> RenewAuthorizationOrThrowAsync(OrderPayment payment, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await _gateway.ReauthorizeAsync(payment.AuthorizationId!, payment.Amount, payment.PaymentReference + "-RA", cancellationToken);
            payment.UpdateAuthorization(snapshot.AuthorizationId, snapshot.Status, snapshot.ExpiresAt);
            await _paymentRepository.UpdateAsync(payment, cancellationToken);
            _logger.LogInformation($"Renewed authorization for order {payment.OrderId}: {snapshot.AuthorizationId} ({snapshot.Status}).");
            return snapshot.AuthorizationId;
        }
        catch (PaymentGatewayException ex)
        {
            var detail = ex.PayPalIssue ?? ex.Message;
            throw new PaymentConflictException(
                $"The authorization for order {payment.OrderId} has expired and can no longer be renewed; the shopper must be charged again. (PayPal: {detail})");
        }
    }

    private static bool IsExpiredAuthorization(PaymentGatewayException ex) =>
        ex.PayPalIssue is not null && ExpiredAuthIssues.Contains(ex.PayPalIssue);

    private async Task<(CardInput? card, string? vaultId)> ResolvePaymentSourceAsync(string buyerId, PayInput input, CancellationToken cancellationToken)
    {
        if (input.SavedPaymentMethodId is { } paymentMethodId)
        {
            var card = await _savedCardRepository.GetByIdAsync(paymentMethodId, cancellationToken);
            if (card is null || card.BuyerId != buyerId)
            {
                // Surfaced as "not found" so one shopper cannot probe or use another's card.
                throw new PaymentNotFoundException($"Saved payment method {paymentMethodId} was not found.");
            }
            return (null, card.PayPalVaultId);
        }

        if (input.Card is not null)
        {
            return (input.Card, null);
        }

        throw new PaymentValidationException("Provide either card details or the id of a saved payment method.");
    }

    private async Task<OrderPayment> LoadPaymentAsync(int orderId, CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.FirstOrDefaultAsync(new OrderPaymentByOrderIdSpecification(orderId), cancellationToken);
        if (payment is null)
        {
            throw new PaymentNotFoundException($"Order {orderId} was not found.");
        }
        return payment;
    }

    private async Task<OrderPayment> LoadOwnedPaymentAsync(int orderId, string buyerId, CancellationToken cancellationToken)
    {
        var payment = await LoadPaymentAsync(orderId, cancellationToken);
        if (payment.BuyerId != buyerId)
        {
            throw new PaymentNotFoundException($"Order {orderId} was not found.");
        }
        return payment;
    }

    private async Task<OrderPaymentView> BuildViewAsync(OrderPayment payment, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.FirstOrDefaultAsync(new OrderWithItemsByIdSpec(payment.OrderId), cancellationToken);
        return OrderPaymentView.From(payment, order?.OrderDate ?? payment.CreatedAt, BuildItems(order));
    }

    private static IReadOnlyList<OrderLineView> BuildItems(Order? order)
    {
        if (order is null)
        {
            return Array.Empty<OrderLineView>();
        }
        return order.OrderItems
            .Select(oi => new OrderLineView(oi.ItemOrdered.CatalogItemId, oi.ItemOrdered.ProductName, oi.UnitPrice, oi.Units))
            .ToList();
    }

    private static RefundLineView ToRefundLine(PaymentRefund r) =>
        new(r.Id, r.PayPalRefundId, r.Amount, r.Status, r.CreatedAt);

    private static Address BuildAddress(ShippingAddressInput? address)
    {
        if (address is null)
        {
            return new Address("N/A", "N/A", "N/A", "N/A", "00000");
        }
        return new Address(
            string.IsNullOrWhiteSpace(address.Street) ? "N/A" : address.Street,
            string.IsNullOrWhiteSpace(address.City) ? "N/A" : address.City,
            string.IsNullOrWhiteSpace(address.State) ? "N/A" : address.State,
            string.IsNullOrWhiteSpace(address.Country) ? "N/A" : address.Country,
            string.IsNullOrWhiteSpace(address.ZipCode) ? "00000" : address.ZipCode);
    }
}
