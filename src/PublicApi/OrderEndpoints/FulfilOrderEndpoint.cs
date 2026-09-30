using System.Threading.Tasks;
using Ardalis.Result;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Operator action: marks an order fulfilled, which is when PayPal actually takes the held funds
/// (captures the authorization). A stale authorization is renewed automatically before capture; if it
/// can no longer be renewed, this fails with an actionable message rather than silently succeeding.
/// </summary>
public class FulfilOrderEndpoint : IEndpoint<IResult, FulfilOrderRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app) =>
        app.MapPost("api/orders/{orderId}/fulfil",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, IPaymentService paymentService) =>
            {
                return await HandleAsync(new FulfilOrderRequest(orderId), paymentService);
            })
            .Produces<FulfilOrderResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status422UnprocessableEntity)
            .WithTags("OrderEndpoints");

    public async Task<IResult> HandleAsync(FulfilOrderRequest request, IPaymentService paymentService)
    {
        var response = new FulfilOrderResponse(request.CorrelationId());

        var result = await paymentService.FulfilAsync(request.OrderId);
        if (result.Status != ResultStatus.Ok)
        {
            return result.ToErrorResult();
        }

        response.Order = OrderDto.FromDomain(result.Value);
        return Results.Ok(response);
    }
}
