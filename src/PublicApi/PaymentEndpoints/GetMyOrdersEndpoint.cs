using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
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

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

/// <summary>GET /api/my-orders — the caller's own orders with their payment state. (any authenticated shopper)</summary>
public class GetMyOrdersEndpoint : IEndpoint<IResult, GetMyOrdersRequest, IReadRepository<Order>>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/my-orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IReadRepository<Order> repository, ClaimsPrincipal user, CancellationToken ct) =>
                await HandleAsync(new GetMyOrdersRequest(user.GetBuyerId()), repository, ct))
            .Produces<List<OrderPaymentDto>>()
            .WithTags("OrderEndpoints");
    }

    public Task<IResult> HandleAsync(GetMyOrdersRequest request, IReadRepository<Order> repository) =>
        HandleAsync(request, repository, default);

    public async Task<IResult> HandleAsync(GetMyOrdersRequest request, IReadRepository<Order> repository, CancellationToken ct)
    {
        var orders = await repository.ListAsync(new CustomerOrdersWithPaymentSpecification(request.BuyerId), ct);
        var result = orders.Select(o => o.ToDto()).ToList();
        return Results.Ok(result);
    }
}

public record GetMyOrdersRequest(string BuyerId);
