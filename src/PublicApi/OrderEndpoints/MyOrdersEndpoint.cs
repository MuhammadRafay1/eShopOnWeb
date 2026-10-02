using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>The caller's own orders with their payment state.</summary>
public class MyOrdersEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/my-orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IRepository<Order> orderRepository) =>
            {
                var buyerId = user.FindFirstValue(ClaimTypes.Name)!;
                return await HandleAsync(buyerId, orderRepository);
            })
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(string buyerId, IRepository<Order> orderRepository)
    {
        var orders = await orderRepository.ListAsync(new CustomerOrdersWithPaymentSpecification(buyerId));

        var response = orders.Select(o => new
        {
            orderId = o.Id,
            orderDate = o.OrderDate,
            total = o.Total(),
            status = o.Status.ToString(),
            payment = o.Payment is null
                ? null
                : new
                {
                    authorizationId = o.Payment.AuthorizationId,
                    authorizationStatus = o.Payment.AuthorizationStatus,
                    captureId = o.Payment.CaptureId,
                    captureStatus = o.Payment.CaptureStatus,
                    capturedAmount = o.Payment.CapturedAmount,
                    payPalFee = o.Payment.PayPalFee,
                    netAmount = o.Payment.NetAmount,
                    refundedAmount = o.Payment.RefundedAmount
                }
        });

        return Results.Ok(response);
    }
}
