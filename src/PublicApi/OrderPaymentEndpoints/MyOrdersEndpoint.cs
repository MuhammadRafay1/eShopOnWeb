using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

/// <summary>The caller's own orders with their payment state.</summary>
public class MyOrdersEndpoint : IEndpoint<IResult, ClaimsPrincipal, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/my-orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IPaymentService paymentService) =>
            {
                return await HandleAsync(user, paymentService);
            })
            .Produces<MyOrdersResponse>()
            .WithTags("OrderPaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(ClaimsPrincipal user, IPaymentService paymentService)
    {
        var buyerId = user.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        var orders = await paymentService.GetOrdersForBuyerAsync(buyerId);

        var response = new MyOrdersResponse
        {
            Orders = orders.Select(o => new OrderPaymentDto
            {
                OrderId = o.OrderId,
                OrderDate = o.OrderDate,
                Total = o.Total,
                Currency = o.CurrencyCode,
                Status = o.Status,
                AuthorizationId = o.AuthorizationId,
                CaptureId = o.CaptureId,
                CapturedAmount = o.CapturedAmount,
                PayPalFee = o.PayPalFee,
                NetAmount = o.NetAmount,
                Refunds = o.Refunds.Select(r => new RefundDto { RefundId = r.RefundId, Amount = r.Amount, Status = r.Status }).ToList()
            }).ToList()
        };

        return Results.Ok(response);
    }
}
