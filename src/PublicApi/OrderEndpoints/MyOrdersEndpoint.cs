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

/// <summary>Lists the caller's own orders, with their payment state.</summary>
public class MyOrdersEndpoint : IEndpoint<IResult, MyOrdersRequest, IRepository<Order>>
{
    public void AddRoute(IEndpointRouteBuilder app) =>
        app.MapGet("api/my-orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IRepository<Order> orderRepository) =>
            {
                return await HandleAsync(new MyOrdersRequest { BuyerId = user.Identity!.Name! }, orderRepository);
            })
            .Produces<MyOrdersResponse>()
            .WithTags("OrderEndpoints");

    public async Task<IResult> HandleAsync(MyOrdersRequest request, IRepository<Order> orderRepository)
    {
        var response = new MyOrdersResponse(request.CorrelationId());

        var orders = await orderRepository.ListAsync(new CustomerOrdersWithPaymentSpecification(request.BuyerId));
        response.Orders = orders.Select(OrderDto.FromDomain).ToList();

        return Results.Ok(response);
    }
}
