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
/// POST api/orders/{orderId}/fulfil — operator marks the order fulfilled; this is when the held money is
/// actually captured. A stale authorization is renewed first. Administrator role only.
/// </summary>
public class FulfilOrderEndpoint : IEndpoint<IResult, int, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/fulfil",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (int orderId, IPaymentService service, CancellationToken ct) => await Run(orderId, service, ct))
            .Produces<OrderPaymentView>()
            .WithTags("OrderEndpoints");
    }

    public Task<IResult> HandleAsync(int orderId, IPaymentService service) => Run(orderId, service, CancellationToken.None);

    private static async Task<IResult> Run(int orderId, IPaymentService service, CancellationToken ct) =>
        Results.Ok(await service.FulfilAsync(orderId, ct));
}
