using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>Returns the caller's own orders together with their payment state.</summary>
public class MyOrdersEndpoint : IEndpoint<IResult, IReadRepository<Order>, IReadRepository<Payment>, HttpContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/my-orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IReadRepository<Order> orderRepository, IReadRepository<Payment> paymentRepository, HttpContext httpContext) =>
            {
                return await HandleAsync(orderRepository, paymentRepository, httpContext);
            })
            .Produces<MyOrdersResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(IReadRepository<Order> orderRepository, IReadRepository<Payment> paymentRepository, HttpContext httpContext)
    {
        var buyerId = httpContext.User.Identity!.Name!;

        var orders = await orderRepository.ListAsync(new CustomerOrdersWithItemsSpecification(buyerId), httpContext.RequestAborted);
        var payments = await paymentRepository.ListAsync(new PaymentsByBuyerSpec(buyerId), httpContext.RequestAborted);
        var paymentsByOrderId = payments.ToDictionary(p => p.OrderId, p => p);

        var response = new MyOrdersResponse
        {
            Orders = orders.Select(order =>
            {
                paymentsByOrderId.TryGetValue(order.Id, out var payment);
                return new MyOrderDto
                {
                    OrderId = order.Id,
                    OrderDate = order.OrderDate,
                    Total = order.Total(),
                    Currency = payment?.CurrencyCode ?? string.Empty,
                    PaymentStatus = order.PaymentStatus.ToString(),
                    AuthorizationId = payment?.AuthorizationId,
                    CaptureId = payment?.CaptureId,
                    CapturedAmount = payment?.CapturedAmount,
                    PayPalFee = payment?.PayPalFee,
                    NetAmount = payment?.NetAmount,
                    Refunds = payment?.Refunds
                        .Select(r => new MyOrderRefundDto { RefundId = r.PayPalRefundId, Amount = r.Amount, Status = r.Status })
                        .ToList() ?? new()
                };
            }).ToList()
        };

        return Results.Ok(response);
    }
}
