using System.Collections.Generic;
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

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// GET api/my-orders — the caller's own orders with their payment state. Shopper-scoped.
/// </summary>
public class GetMyOrdersEndpoint : IEndpoint<IResult, string, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/my-orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ClaimsPrincipal user, IPaymentService service, CancellationToken ct) =>
                await Run(user.Identity!.Name!, service, ct))
            .Produces<IReadOnlyList<OrderPaymentView>>()
            .WithTags("OrderEndpoints");
    }

    public Task<IResult> HandleAsync(string buyerId, IPaymentService service) => Run(buyerId, service, CancellationToken.None);

    private static async Task<IResult> Run(string buyerId, IPaymentService service, CancellationToken ct) =>
        Results.Ok(await service.GetMyOrdersAsync(buyerId, ct));
}
