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
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services.Payments;

public class OrderPaymentService : IOrderPaymentService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<CatalogItem> _catalogItemRepository;
    private readonly IRepository<OrderPayment> _orderPaymentRepository;
    private readonly IRepository<PaymentRefund> _paymentRefundRepository;
    private readonly IRepository<SavedPaymentMethod> _savedPaymentMethodRepository;
    private readonly IPayPalPaymentGateway _gateway;
    private readonly IPaymentSettings _settings;

    public OrderPaymentService(
        IRepository<Order> orderRepository,
        IRepository<CatalogItem> catalogItemRepository,
        IRepository<OrderPayment> orderPaymentRepository,
        IRepository<PaymentRefund> paymentRefundRepository,
        IRepository<SavedPaymentMethod> savedPaymentMethodRepository,
        IPayPalPaymentGateway gateway,
        IPaymentSettings settings)
    {
        _orderRepository = orderRepository;
        _catalogItemRepository = catalogItemRepository;
        _orderPaymentRepository = orderPaymentRepository;
        _paymentRefundRepository = paymentRefundRepository;
        _savedPaymentMethodRepository = savedPaymentMethodRepository;
        _gateway = gateway;
        _settings = settings;
    }

    public async Task<int> PlaceOrderAsync(string buyerId, IReadOnlyList<OrderLineItemRequest> items, ShipToAddressRequest? shipTo, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        if (items is null || items.Count == 0)
        {
            throw new PaymentException("An order must contain at least one item.");
        }

        if (items.Any(i => i.Quantity <= 0))
        {
            throw new PaymentException("Every order item must have a positive quantity.");
        }

        var ids = items.Select(i => i.CatalogItemId).Distinct().ToArray();
        var catalogItems = await _catalogItemRepository.ListAsync(new CatalogItemsSpecification(ids), cancellationToken);
        if (catalogItems.Count != ids.Length)
        {
            throw new PaymentException("One or more catalog items do not exist.");
        }

        var orderItems = items.Select(i =>
        {
            var catalogItem = catalogItems.First(c => c.Id == i.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, catalogItem.PictureUri);
            return new OrderItem(itemOrdered, catalogItem.Price, i.Quantity);
        }).ToList();

        var address = shipTo is null
            ? new Address("N/A", "N/A", "N/A", "N/A", "N/A")
            : new Address(shipTo.Street, shipTo.City, shipTo.State ?? string.Empty, shipTo.Country, shipTo.ZipCode);

        var order = new Order(buyerId, address, orderItems);
        order = await _orderRepository.AddAsync(order, cancellationToken);
        return order.Id;
    }

    public async Task<OrderSummaryView> PayAsync(int orderId, string buyerId, PayWithRequest payWith, CancellationToken cancellationToken)
    {
        if ((payWith.Card is null) == (payWith.SavedPaymentMethodId is null))
        {
            throw new PaymentException("Pay with exactly one of: card details, or a saved payment method id.");
        }

        var order = await _orderRepository.FirstOrDefaultAsync(new OrderWithItemsByIdSpec(orderId), cancellationToken);
        if (order is null || order.BuyerId != buyerId)
        {
            throw new ResourceNotFoundException("Order", orderId);
        }

        PaymentSource source;
        if (payWith.SavedPaymentMethodId is not null)
        {
            var saved = await _savedPaymentMethodRepository.GetByIdAsync(payWith.SavedPaymentMethodId, cancellationToken);
            if (saved is null || saved.BuyerId != buyerId)
            {
                throw new ResourceNotFoundException("SavedPaymentMethod", payWith.SavedPaymentMethodId);
            }

            source = new PaymentSource(null, saved.Id);
        }
        else
        {
            source = new PaymentSource(payWith.Card, null);
        }

        var payment = await ClaimForPayAsync(orderId, order.Total(), cancellationToken);
        if (payment is null)
        {
            // Another request already owns this order's payment and it is not retryable - report current state.
            return await GetOrderForBuyerAsync(orderId, buyerId, cancellationToken);
        }

        try
        {
            var result = await _gateway.AuthorizeOrderAsync(
                new AuthorizeOrderCommand(orderId, payment.InvoiceId, payment.Amount, payment.CurrencyCode, source),
                cancellationToken);
            payment.AuthorizationSucceeded(result.PayPalOrderId, result.AuthorizationId, result.AuthorizationStatus,
                result.AuthorizationExpiryUtc, result.PaymentSourceDescriptor);
        }
        catch
        {
            payment.AuthorizationFailed();
            await _orderPaymentRepository.UpdateAsync(payment, cancellationToken);
            throw;
        }

        await _orderPaymentRepository.UpdateAsync(payment, cancellationToken);
        return ToView(order, payment);
    }

    /// <summary>
    /// Admits this order into an authorization attempt: inserts the OrderPayment row on first pay
    /// (the insert IS the duplicate-pay claim) or transitions an existing AuthorizationFailed row.
    /// Returns null when the order is already paid/in flight/terminal (idempotent no-op for the caller).
    /// </summary>
    private async Task<OrderPayment?> ClaimForPayAsync(int orderId, decimal orderTotal, CancellationToken cancellationToken)
    {
        var existing = await _orderPaymentRepository.GetByIdAsync(orderId, cancellationToken);
        if (existing is null)
        {
            var payment = new OrderPayment(orderId, $"ord-{orderId}", orderTotal, _settings.CurrencyCode);
            try
            {
                return await _orderPaymentRepository.AddAsync(payment, cancellationToken);
            }
            catch
            {
                var reloaded = await _orderPaymentRepository.GetByIdAsync(orderId, cancellationToken);
                if (reloaded is null)
                {
                    throw;
                }

                return reloaded.Status == PaymentStatus.AuthorizationFailed ? await RetryAsync(reloaded, cancellationToken) : null;
            }
        }

        return existing.Status == PaymentStatus.AuthorizationFailed ? await RetryAsync(existing, cancellationToken) : null;

        async Task<OrderPayment?> RetryAsync(OrderPayment payment, CancellationToken ct)
        {
            payment.BeginAuthorizing();
            try
            {
                await _orderPaymentRepository.UpdateAsync(payment, ct);
                return payment;
            }
            catch
            {
                return null; // a racing retry won first - report its outcome instead of paying twice.
            }
        }
    }

    public async Task<OrderSummaryView> FulfilAsync(int orderId, CancellationToken cancellationToken)
    {
        var payment = await _orderPaymentRepository.GetByIdAsync(orderId, cancellationToken);
        if (payment is null)
        {
            throw new ResourceNotFoundException("OrderPayment", orderId);
        }

        if (payment.Status is PaymentStatus.Captured or PaymentStatus.PartiallyRefunded or PaymentStatus.Refunded)
        {
            return await ToAdminViewAsync(orderId, payment, cancellationToken); // idempotent: already fulfilled
        }

        if (payment.Status != PaymentStatus.Authorized)
        {
            throw new PaymentAuthorizationException($"Order {orderId} cannot be fulfilled from status {payment.Status}.");
        }

        payment.BeginFulfilling();
        try
        {
            await _orderPaymentRepository.UpdateAsync(payment, cancellationToken);
        }
        catch
        {
            return await ToAdminViewAsync(orderId, null, cancellationToken); // a racing fulfil won first
        }

        var authorizationId = payment.AuthorizationId!;
        CaptureResult captureResult;
        try
        {
            captureResult = await _gateway.CaptureAsync(new CaptureCommand(orderId, authorizationId, payment.InvoiceId), cancellationToken);
        }
        catch (AuthorizationStaleException)
        {
            captureResult = await RenewAndCaptureAsync(orderId, payment, cancellationToken);
        }
        catch
        {
            payment.FulfillmentFailed();
            await _orderPaymentRepository.UpdateAsync(payment, cancellationToken);
            throw;
        }

        payment.CaptureSucceeded(captureResult.CaptureId, captureResult.CaptureStatus, captureResult.CapturedGross,
            captureResult.Fee, captureResult.NetAmount);
        await _orderPaymentRepository.UpdateAsync(payment, cancellationToken);
        return await ToAdminViewAsync(orderId, payment, cancellationToken);
    }

    private async Task<CaptureResult> RenewAndCaptureAsync(int orderId, OrderPayment payment, CancellationToken cancellationToken)
    {
        ReauthorizeResult reauth;
        try
        {
            reauth = await _gateway.ReauthorizeAsync(
                new ReauthorizeCommand(payment.AuthorizationId!, payment.Amount, payment.CurrencyCode), cancellationToken);
        }
        catch
        {
            payment.FulfillmentFailed();
            await _orderPaymentRepository.UpdateAsync(payment, cancellationToken);
            throw new AuthorizationExpiredException(orderId);
        }

        payment.RenewAuthorization(reauth.AuthorizationId, reauth.Status, reauth.ExpiryUtc);
        await _orderPaymentRepository.UpdateAsync(payment, cancellationToken);

        try
        {
            return await _gateway.CaptureAsync(new CaptureCommand(orderId, reauth.AuthorizationId, payment.InvoiceId), cancellationToken);
        }
        catch
        {
            payment.FulfillmentFailed();
            await _orderPaymentRepository.UpdateAsync(payment, cancellationToken);
            throw;
        }
    }

    public async Task<OrderSummaryView> CancelAsync(int orderId, CancellationToken cancellationToken)
    {
        var payment = await _orderPaymentRepository.GetByIdAsync(orderId, cancellationToken);
        if (payment is null)
        {
            throw new ResourceNotFoundException("OrderPayment", orderId);
        }

        if (payment.Status == PaymentStatus.Cancelled)
        {
            return await ToAdminViewAsync(orderId, payment, cancellationToken); // idempotent
        }

        if (payment.Status != PaymentStatus.Authorized)
        {
            throw new PaymentAuthorizationException($"Order {orderId} cannot be cancelled from status {payment.Status}.");
        }

        var authorizationId = payment.AuthorizationId!;
        payment.Cancel();
        try
        {
            await _orderPaymentRepository.UpdateAsync(payment, cancellationToken);
        }
        catch
        {
            return await ToAdminViewAsync(orderId, null, cancellationToken); // a racing cancel/fulfil won first
        }

        await _gateway.VoidAsync(authorizationId, cancellationToken);
        return await ToAdminViewAsync(orderId, payment, cancellationToken);
    }

    public async Task<RefundOutcome> RefundAsync(int orderId, decimal? amount, string idempotencyKey, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(idempotencyKey, nameof(idempotencyKey));

        var payment = await _orderPaymentRepository.GetByIdAsync(orderId, cancellationToken);
        if (payment is null)
        {
            throw new ResourceNotFoundException("OrderPayment", orderId);
        }

        if (payment.Status != PaymentStatus.Captured && payment.Status != PaymentStatus.PartiallyRefunded)
        {
            throw new PaymentAuthorizationException($"Order {orderId} cannot be refunded from status {payment.Status}.");
        }

        var refundAmount = amount ?? payment.RemainingRefundable;
        if (refundAmount <= 0 || refundAmount > payment.RemainingRefundable + 0.005m)
        {
            throw new RefundAmountExceededException(orderId, refundAmount, payment.RemainingRefundable);
        }

        var captureId = payment.CaptureId!;
        var refund = new PaymentRefund(idempotencyKey, orderId, captureId, refundAmount);
        try
        {
            refund = await _paymentRefundRepository.AddAsync(refund, cancellationToken);
        }
        catch
        {
            var existing = await _paymentRefundRepository.GetByIdAsync(idempotencyKey, cancellationToken);
            if (existing is null)
            {
                throw;
            }

            // Same idempotency key as before: replay the recorded outcome rather than refunding again.
            return new RefundOutcome(existing.IdempotencyKey, existing.Status, existing.Amount);
        }

        RefundResult result;
        try
        {
            result = await _gateway.RefundAsync(
                new RefundCommand(captureId, amount, payment.CurrencyCode, idempotencyKey), cancellationToken);
        }
        catch
        {
            refund.Completed(string.Empty, "Failed");
            await _paymentRefundRepository.UpdateAsync(refund, cancellationToken);
            throw;
        }

        refund.Completed(result.RefundId, result.Status);
        await _paymentRefundRepository.UpdateAsync(refund, cancellationToken);

        payment.RecordRefund(refundAmount);
        await _orderPaymentRepository.UpdateAsync(payment, cancellationToken);

        return new RefundOutcome(refund.IdempotencyKey, refund.Status, refund.Amount);
    }

    public async Task<IReadOnlyList<OrderSummaryView>> GetOrdersForBuyerAsync(string buyerId, CancellationToken cancellationToken)
    {
        var orders = await _orderRepository.ListAsync(new CustomerOrdersWithItemsSpecification(buyerId), cancellationToken);
        var views = new List<OrderSummaryView>(orders.Count);
        foreach (var order in orders)
        {
            var payment = await _orderPaymentRepository.GetByIdAsync(order.Id, cancellationToken);
            views.Add(ToView(order, payment));
        }

        return views;
    }

    public async Task<OrderSummaryView> GetOrderForBuyerAsync(int orderId, string buyerId, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.FirstOrDefaultAsync(new OrderWithItemsByIdSpec(orderId), cancellationToken);
        if (order is null || order.BuyerId != buyerId)
        {
            throw new ResourceNotFoundException("Order", orderId);
        }

        var payment = await _orderPaymentRepository.GetByIdAsync(orderId, cancellationToken);
        return ToView(order, payment);
    }

    private async Task<OrderSummaryView> ToAdminViewAsync(int orderId, OrderPayment? payment, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.FirstOrDefaultAsync(new OrderWithItemsByIdSpec(orderId), cancellationToken);
        Guard.Against.Null(order, nameof(order));
        payment ??= await _orderPaymentRepository.GetByIdAsync(orderId, cancellationToken);
        return ToView(order, payment);
    }

    private static OrderSummaryView ToView(Order order, OrderPayment? payment)
    {
        var items = order.OrderItems.Select(i => new OrderLineItemView(
            i.ItemOrdered.CatalogItemId, i.ItemOrdered.ProductName, i.UnitPrice, i.Units)).ToList();

        return new OrderSummaryView(
            order.Id,
            order.OrderDate,
            order.Total(),
            items,
            (payment?.Status ?? PaymentStatus.AwaitingPayment).ToString(),
            payment?.AuthorizationId,
            payment?.CaptureId,
            payment?.CapturedGross,
            payment?.PayPalFee,
            payment?.NetAmount,
            payment?.RefundedTotal ?? 0m);
    }
}
