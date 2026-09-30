using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

/// <summary>
/// Lists the caller's own orders together with their payment state.
/// </summary>
public class MyOrdersEndpoint : IEndpoint<IResult, MyOrdersRequest, IOrderPaymentService>
{
    private readonly ICurrencyProvider _currencyProvider;

    public MyOrdersEndpoint(ICurrencyProvider currencyProvider)
    {
        _currencyProvider = currencyProvider;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/my-orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IOrderPaymentService orderPaymentService) =>
                await HandleAsync(new MyOrdersRequest(user.Identity!.Name!), orderPaymentService))
            .Produces<MyOrdersResponse>()
            .WithTags("OrderPaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(MyOrdersRequest request, IOrderPaymentService orderPaymentService)
    {
        var response = new MyOrdersResponse(request.CorrelationId());

        var orders = await orderPaymentService.GetOrdersForBuyerAsync(request.BuyerId, CancellationToken.None);

        response.Orders = orders.Select(o => new OrderSummaryDto
        {
            OrderId = o.Id,
            OrderDate = o.OrderDate,
            Total = o.Total(),
            Currency = o.Payment?.CurrencyCode ?? _currencyProvider.CurrencyCode,
            PaymentStatus = o.PaymentStatus.ToString(),
            AuthorizationId = o.Payment?.AuthorizationId,
            CaptureId = o.Payment?.CaptureId,
            CapturedAmount = o.Payment?.CapturedAmount,
            PayPalFee = o.Payment?.PayPalFee,
            NetAmount = o.Payment?.NetAmount,
            Refunds = o.Payment?.Refunds.Select(r => new RefundSummaryDto { RefundId = r.Id, Amount = r.Amount, Status = r.Status }).ToList() ?? new(),
            Items = o.OrderItems.Select(i => new OrderItemSummaryDto
            {
                CatalogItemId = i.ItemOrdered.CatalogItemId,
                ProductName = i.ItemOrdered.ProductName,
                UnitPrice = i.UnitPrice,
                Units = i.Units
            }).ToList()
        }).ToList();

        return Results.Ok(response);
    }
}
