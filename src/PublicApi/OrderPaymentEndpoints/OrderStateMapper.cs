using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

internal static class OrderStateMapper
{
    public static OrderStateDto ToDto(OrderSummaryView view) => new()
    {
        OrderId = view.OrderId,
        OrderDate = view.OrderDate,
        Total = view.Total,
        Items = view.Items.Select(i => new OrderLineItemDto
        {
            CatalogItemId = i.CatalogItemId,
            ProductName = i.ProductName,
            UnitPrice = i.UnitPrice,
            Units = i.Units
        }).ToList(),
        PaymentStatus = view.PaymentStatus,
        AuthorizationId = view.AuthorizationId,
        CaptureId = view.CaptureId,
        CapturedGross = view.CapturedGross,
        PayPalFee = view.PayPalFee,
        NetAmount = view.NetAmount,
        RefundedTotal = view.RefundedTotal
    };
}
