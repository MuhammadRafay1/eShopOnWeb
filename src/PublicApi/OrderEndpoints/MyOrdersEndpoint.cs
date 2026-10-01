using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderPaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.PublicApi.PaymentEndpoints;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>The caller's own orders with their payment state.</summary>
public class MyOrdersEndpoint : IEndpoint<IResult, string, IReadRepository<Order>, IReadRepository<OrderPayment>>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/my-orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IReadRepository<Order> orderRepository, IReadRepository<OrderPayment> paymentRepository) =>
            {
                return await HandleAsync(user.Identity!.Name!, orderRepository, paymentRepository);
            })
            .Produces<MyOrdersResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(string buyerId, IReadRepository<Order> orderRepository, IReadRepository<OrderPayment> paymentRepository)
    {
        var orders = await orderRepository.ListAsync(new CustomerOrdersWithItemsSpecification(buyerId));
        var payments = await paymentRepository.ListAsync(new OrderPaymentsByBuyerSpec(buyerId));
        var paymentsByOrderId = payments.ToDictionary(p => p.OrderId);

        var dtos = orders.Select(order => new MyOrderDto
        {
            OrderId = order.Id,
            OrderDate = order.OrderDate,
            Total = order.Total(),
            Payment = paymentsByOrderId.TryGetValue(order.Id, out var payment) ? OrderPaymentDto.From(payment) : null
        }).ToArray();

        var response = new MyOrdersResponse(Guid.NewGuid()) { Orders = dtos };
        return Results.Ok(response);
    }
}
