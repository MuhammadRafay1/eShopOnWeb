using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

/// <summary>Operator action: captures the authorized amount, renewing a stale authorization first if needed.</summary>
public class FulfilOrderEndpoint : IEndpoint<IResult, FulfilOrderRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/fulfil",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, IPaymentService paymentService) =>
            {
                return await HandleAsync(new FulfilOrderRequest(orderId), paymentService);
            })
            .Produces<FulfilOrderResponse>()
            .WithTags("OrderPaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(FulfilOrderRequest request, IPaymentService paymentService)
    {
        var outcome = await paymentService.FulfilAsync(request.OrderId);
        if (outcome is null)
        {
            return Results.NotFound();
        }

        var response = new FulfilOrderResponse(request.CorrelationId())
        {
            OrderId = outcome.OrderId,
            Status = outcome.Status,
            CaptureId = outcome.CaptureId,
            CapturedAmount = outcome.CapturedAmount,
            PayPalFee = outcome.PayPalFee,
            NetAmount = outcome.NetAmount,
            Currency = outcome.CurrencyCode
        };

        return Results.Ok(response);
    }
}
