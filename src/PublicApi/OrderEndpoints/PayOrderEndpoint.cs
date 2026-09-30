using System.Security.Claims;
using System.Threading.Tasks;
using Ardalis.Result;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.PayPal;
using MinimalApi.Endpoint;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Authorizes (holds) the order total with PayPal - either a one-off card or one of the shopper's
/// saved cards. Does not take the money yet; see FulfilOrderEndpoint for the capture.
/// </summary>
public class PayOrderEndpoint : IEndpoint<IResult, PayOrderRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app) =>
        app.MapPost("api/orders/{orderId}/pay",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, PayOrderRequest request, ClaimsPrincipal user, IPaymentService paymentService) =>
            {
                request.OrderId = orderId;
                request.BuyerId = user.Identity!.Name!;
                return await HandleAsync(request, paymentService);
            })
            .Produces<PayOrderResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("OrderEndpoints");

    public async Task<IResult> HandleAsync(PayOrderRequest request, IPaymentService paymentService)
    {
        var response = new PayOrderResponse(request.CorrelationId());

        var instrument = new PaymentInstrument(request.Card?.ToDomain(), request.SavedPaymentMethodId);
        var result = await paymentService.AuthorizeAsync(request.OrderId, request.BuyerId, instrument);
        if (result.Status != ResultStatus.Ok)
        {
            return result.ToErrorResult();
        }

        response.Order = OrderDto.FromDomain(result.Value);
        return Results.Ok(response);
    }
}
