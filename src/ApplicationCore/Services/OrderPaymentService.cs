using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.SavedCardAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Payments;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class OrderPaymentService : IOrderPaymentService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IReadRepository<CatalogItem> _catalogItemRepository;
    private readonly IReadRepository<SavedCard> _savedCardRepository;
    private readonly IPaymentGateway _gateway;
    private readonly ICurrencyProvider _currencyProvider;

    public OrderPaymentService(
        IRepository<Order> orderRepository,
        IReadRepository<CatalogItem> catalogItemRepository,
        IReadRepository<SavedCard> savedCardRepository,
        IPaymentGateway gateway,
        ICurrencyProvider currencyProvider)
    {
        _orderRepository = orderRepository;
        _catalogItemRepository = catalogItemRepository;
        _savedCardRepository = savedCardRepository;
        _gateway = gateway;
        _currencyProvider = currencyProvider;
    }

    public async Task<Order> PlaceOrderAsync(string buyerId, IReadOnlyList<OrderLineRequest> items, Address? shipToAddress, CancellationToken ct)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        if (items is null || items.Count == 0)
        {
            throw new ArgumentException("An order must contain at least one item.");
        }

        var ids = items.Select(i => i.CatalogItemId).Distinct().ToArray();
        var catalogItems = await _catalogItemRepository.ListAsync(new CatalogItemsSpecification(ids), ct);

        var orderItems = new List<OrderItem>();
        foreach (var line in items)
        {
            if (line.Quantity < 1)
            {
                throw new ArgumentException($"Quantity must be at least 1 for catalog item {line.CatalogItemId}.");
            }

            var catalogItem = catalogItems.FirstOrDefault(c => c.Id == line.CatalogItemId);
            if (catalogItem is null)
            {
                throw new ArgumentException($"Unknown catalog item id {line.CatalogItemId}.");
            }

            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, catalogItem.PictureUri);
            orderItems.Add(new OrderItem(itemOrdered, catalogItem.Price, line.Quantity));
        }

        var address = shipToAddress ?? new Address("N/A", "N/A", "N/A", "N/A", "00000");
        var order = new Order(buyerId, address, orderItems);
        return await _orderRepository.AddAsync(order, ct);
    }

    public async Task<Order?> GetOrderForBuyerAsync(int orderId, string buyerId, CancellationToken ct)
        => await _orderRepository.FirstOrDefaultAsync(new OrderWithPaymentByIdForBuyerSpec(orderId, buyerId), ct);

    public async Task<IReadOnlyList<Order>> GetOrdersForBuyerAsync(string buyerId, CancellationToken ct)
        => await _orderRepository.ListAsync(new CustomerOrdersWithPaymentSpec(buyerId), ct);

    public async Task<Order?> PayAsync(int orderId, string buyerId, CardInput? card, int? savedPaymentMethodId, CancellationToken ct)
    {
        if ((card is null) == (savedPaymentMethodId is null))
        {
            throw new ArgumentException("Exactly one of card or savedPaymentMethodId must be provided.");
        }

        var order = await _orderRepository.FirstOrDefaultAsync(new OrderWithPaymentByIdForBuyerSpec(orderId, buyerId), ct);
        if (order is null)
        {
            return null;
        }

        if (order.PaymentStatus == OrderPaymentStatus.Authorized)
        {
            return order; // idempotent no-op: a double-click never authorizes twice
        }

        if (order.PaymentStatus != OrderPaymentStatus.AwaitingPayment)
        {
            throw new InvalidOrderStateException($"Order {orderId} cannot be paid while in status {order.PaymentStatus}.");
        }

        var vaultId = string.Empty;
        if (savedPaymentMethodId.HasValue)
        {
            var savedCard = await _savedCardRepository.FirstOrDefaultAsync(new SavedCardByIdForBuyerSpec(savedPaymentMethodId.Value, buyerId), ct);
            if (savedCard is null)
            {
                throw new PaymentMethodNotFoundException($"Saved card {savedPaymentMethodId} was not found.");
            }

            vaultId = savedCard.PayPalVaultId;
        }

        var currency = _currencyProvider.CurrencyCode;
        var total = order.Total();
        var invoiceReference = $"eshop-{orderId}-{Guid.NewGuid():N}";

        var createdOrder = await _gateway.CreateAuthorizeOrderAsync(total, currency, invoiceReference, $"order-{orderId}-create", ct);

        var authorization = savedPaymentMethodId.HasValue
            ? await _gateway.AuthorizeWithVaultAsync(createdOrder.PayPalOrderId, vaultId, $"order-{orderId}-authorize", ct)
            : await _gateway.AuthorizeWithCardAsync(createdOrder.PayPalOrderId, card!, $"order-{orderId}-authorize", ct);

        var payment = new Payment(orderId, "PayPal", currency, total, invoiceReference);
        payment.SetCreatedOrder(createdOrder.PayPalOrderId);
        payment.SetAuthorization(authorization.AuthorizationId, authorization.Status, authorization.ExpiresAt);

        order.AttachAuthorization(payment);
        await _orderRepository.UpdateAsync(order, ct);

        return order;
    }

    public async Task<Order?> FulfilAsync(int orderId, CancellationToken ct)
    {
        var order = await _orderRepository.FirstOrDefaultAsync(new OrderWithPaymentByIdSpec(orderId), ct);
        if (order is null)
        {
            return null;
        }

        if (order.PaymentStatus == OrderPaymentStatus.Fulfilled)
        {
            return order; // idempotent no-op: a double-click never captures twice
        }

        if (order.PaymentStatus != OrderPaymentStatus.Authorized)
        {
            throw new InvalidOrderStateException($"Order {orderId} cannot be fulfilled while in status {order.PaymentStatus}.");
        }

        var payment = order.Payment!;

        if (payment.AuthorizationExpiresAt is { } expiresAt && expiresAt <= DateTimeOffset.UtcNow)
        {
            await ReauthorizeAsync(order, payment, ct);
        }

        GatewayCapture capture;
        try
        {
            capture = await _gateway.CaptureAsync(payment.AuthorizationId!, $"order-{orderId}-capture-{payment.AuthorizationId}", ct);
        }
        catch (PaymentGatewayException)
        {
            await ReauthorizeAsync(order, payment, ct);
            capture = await _gateway.CaptureAsync(payment.AuthorizationId!, $"order-{orderId}-capture-{payment.AuthorizationId}", ct);
        }

        payment.SetCapture(capture.CaptureId, capture.Status, capture.GrossAmount, capture.PayPalFee, capture.NetAmount);
        order.MarkFulfilled();
        await _orderRepository.UpdateAsync(order, ct);

        return order;
    }

    private async Task ReauthorizeAsync(Order order, Payment payment, CancellationToken ct)
    {
        try
        {
            var reauth = await _gateway.ReauthorizeAsync(
                payment.AuthorizationId!,
                payment.AuthorizedAmount,
                payment.CurrencyCode,
                $"order-{order.Id}-reauth-{payment.AuthorizationId}",
                ct);
            payment.SetAuthorization(reauth.AuthorizationId, reauth.Status, reauth.ExpiresAt);
        }
        catch (PaymentGatewayException ex)
        {
            throw new AuthorizationNotRenewableException(
                $"The authorization for order {order.Id} has expired and can no longer be renewed ({ex.Message}). Ask the shopper to pay the order again.");
        }
    }

    public async Task<Order?> CancelAsync(int orderId, CancellationToken ct)
    {
        var order = await _orderRepository.FirstOrDefaultAsync(new OrderWithPaymentByIdSpec(orderId), ct);
        if (order is null)
        {
            return null;
        }

        if (order.PaymentStatus == OrderPaymentStatus.Cancelled)
        {
            return order; // idempotent no-op
        }

        if (order.PaymentStatus != OrderPaymentStatus.AwaitingPayment && order.PaymentStatus != OrderPaymentStatus.Authorized)
        {
            throw new InvalidOrderStateException($"Order {orderId} cannot be cancelled while in status {order.PaymentStatus}. Use refunds after fulfilment.");
        }

        if (order.Payment?.AuthorizationId is not null)
        {
            await _gateway.VoidAsync(order.Payment.AuthorizationId, ct);
        }

        order.MarkCancelled();
        await _orderRepository.UpdateAsync(order, ct);

        return order;
    }

    public async Task<(Order Order, PaymentRefund Refund)?> RefundAsync(int orderId, string buyerId, decimal? amount, string idempotencyKey, CancellationToken ct)
    {
        Guard.Against.NullOrEmpty(idempotencyKey, nameof(idempotencyKey));

        var order = await _orderRepository.FirstOrDefaultAsync(new OrderWithPaymentByIdForBuyerSpec(orderId, buyerId), ct);
        if (order is null)
        {
            return null;
        }

        if (order.PaymentStatus != OrderPaymentStatus.Fulfilled && order.PaymentStatus != OrderPaymentStatus.PartiallyRefunded)
        {
            throw new InvalidOrderStateException($"Order {orderId} cannot be refunded while in status {order.PaymentStatus}.");
        }

        var payment = order.Payment!;

        var existing = payment.Refunds.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey);
        if (existing is not null)
        {
            return (order, existing); // same idempotency key never refunds twice
        }

        if (amount.HasValue && amount.Value <= 0)
        {
            throw new ArgumentException("Refund amount must be positive.");
        }

        var remaining = payment.RefundableRemaining();
        if (amount.HasValue && amount.Value > remaining)
        {
            throw new RefundAmountExceedsRemainingException(
                $"Refund amount {amount.Value} exceeds the refundable remaining {remaining} {payment.CurrencyCode} for order {orderId}.");
        }

        var gatewayRefund = await _gateway.RefundAsync(payment.CaptureId!, amount, payment.CurrencyCode, idempotencyKey, ct);

        var refund = new PaymentRefund(payment.Id, gatewayRefund.RefundId, gatewayRefund.Amount, gatewayRefund.Status, idempotencyKey);
        payment.AddRefund(refund);
        order.ApplyRefund();
        await _orderRepository.UpdateAsync(order, ct);

        return (order, refund);
    }
}
