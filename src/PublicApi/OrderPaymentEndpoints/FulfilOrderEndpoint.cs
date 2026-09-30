using System.Threading;
using System.Threading.Tasks;
using BlazorShared.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

/// <summary>
/// Operator action: captures a held authorization. This is when the money is actually taken.
/// </summary>
public class FulfilOrderEndpoint : IEndpoint<IResult, FulfilOrderRequest, IOrderPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/fulfil",
            [Authorize(Roles = Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, IOrderPaymentService orderPaymentService) =>
                await HandleAsync(new FulfilOrderRequest(orderId), orderPaymentService))
            .Produces<FulfilOrderResponse>()
            .WithTags("OrderPaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(FulfilOrderRequest request, IOrderPaymentService orderPaymentService)
    {
        var order = await orderPaymentService.FulfilAsync(request.OrderId, CancellationToken.None);
        if (order is null)
        {
            return Results.NotFound();
        }

        var response = new FulfilOrderResponse(request.CorrelationId())
        {
            OrderId = order.Id,
            PaymentStatus = order.PaymentStatus.ToString(),
            CaptureId = order.Payment?.CaptureId,
            CapturedAmount = order.Payment?.CapturedAmount,
            PayPalFee = order.Payment?.PayPalFee,
            NetAmount = order.Payment?.NetAmount,
            Currency = order.Payment?.CurrencyCode ?? string.Empty
        };

        return Results.Ok(response);
    }
}
