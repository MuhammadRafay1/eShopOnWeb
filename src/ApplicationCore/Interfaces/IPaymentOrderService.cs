using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public record PlaceOrderResult(int OrderId, decimal Total, string Currency, string Status);

/// <summary>
/// Places an order directly from catalog item ids/quantities (reusing the existing Order/OrderItem
/// model) and opens an AwaitingPayment Payment record for it. This is the PublicApi-only order
/// path used by the payments flow; it does not touch the Web storefront's basket checkout.
/// </summary>
public interface IPaymentOrderService
{
    Task<PlaceOrderResult> PlaceOrderAsync(string buyerId, IEnumerable<(int CatalogItemId, int Quantity)> items, Address? shipToAddress, CancellationToken ct = default);
}
