using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public record OrderLineRequest(int CatalogItemId, int Quantity);

/// <summary>
/// Orchestrates the PayPal calls, order/payment state transitions and persistence for the payment
/// surface (Flow 1 and Flow 2 of the PayPal integration). Endpoints stay thin; this is where the
/// business logic lives so it can be unit tested against a faked IPayPalClient.
/// </summary>
public interface IPaymentService
{
    Task<Order> CreateOrderAsync(string buyerId, IReadOnlyList<OrderLineRequest> lines, Address? shipToAddress);

    Task<Payment> PayWithCardAsync(int orderId, string buyerId, CardDetails card);

    Task<Payment> PayWithSavedCardAsync(int orderId, string buyerId, int savedPaymentMethodId);

    Task<Payment> FulfilAsync(int orderId);

    Task<Payment> CancelAsync(int orderId);

    Task<(Payment Payment, Refund Refund)> RefundAsync(int orderId, string buyerId, decimal? amount, string idempotencyKey, string? note);

    Task<IReadOnlyList<(Order Order, Payment? Payment)>> GetOrdersForBuyerAsync(string buyerId);

    Task<SavedPaymentMethod> SavePaymentMethodAsync(string ownerId, CardDetails card);

    Task<IReadOnlyList<SavedPaymentMethod>> GetPaymentMethodsAsync(string ownerId);

    Task DeletePaymentMethodAsync(string ownerId, int paymentMethodId);

    Task<ReconciliationReport> ReconcileAsync(DateTimeOffset from, DateTimeOffset to);
}
