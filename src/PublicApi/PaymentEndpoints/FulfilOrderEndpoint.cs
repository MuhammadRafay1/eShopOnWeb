using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

/// <summary>POST /api/orders/{orderId}/fulfil — mark fulfilled and capture the held funds. (operator/admin)</summary>
public class FulfilOrderEndpoint : IEndpoint<IResult, FulfilOrderRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId:int}/fulfil",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, IPaymentService service, CancellationToken ct) =>
                await HandleAsync(new FulfilOrderRequest(orderId), service, ct))
            .Produces<OrderPaymentDto>()
            .WithTags("OrderEndpoints");
    }

    public Task<IResult> HandleAsync(FulfilOrderRequest request, IPaymentService service) =>
        HandleAsync(request, service, default);

    public async Task<IResult> HandleAsync(FulfilOrderRequest request, IPaymentService service, CancellationToken ct)
    {
        var order = await service.FulfilAsync(request.OrderId, ct);
        return Results.Ok(order.ToDto());
    }
}

public record FulfilOrderRequest(int OrderId);
