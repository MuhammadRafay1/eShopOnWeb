using System;
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

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

/// <summary>
/// Authorizes (holds) the order total with a one-off card or a saved card. Does not capture funds.
/// </summary>
public class PayOrderEndpoint : IEndpoint<IResult, PayOrderRequest, IOrderPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/pay",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, PayOrderRequest request, ClaimsPrincipal user, IOrderPaymentService orderPaymentService) =>
            {
                request.OrderId = orderId;
                request.BuyerId = user.Identity!.Name!;
                return await HandleAsync(request, orderPaymentService);
            })
            .Produces<PayOrderResponse>()
            .WithTags("OrderPaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(PayOrderRequest request, IOrderPaymentService orderPaymentService)
    {
        if ((request.Card is null) == (request.SavedPaymentMethodId is null))
        {
            return Results.BadRequest("Provide exactly one of card or savedPaymentMethodId.");
        }

        var order = await orderPaymentService.PayAsync(
            request.OrderId,
            request.BuyerId,
            request.Card?.ToCardInput(),
            request.SavedPaymentMethodId,
            CancellationToken.None);

        if (order is null)
        {
            return Results.NotFound();
        }

        var response = new PayOrderResponse(request.CorrelationId())
        {
            OrderId = order.Id,
            PaymentStatus = order.PaymentStatus.ToString(),
            AuthorizationId = order.Payment?.AuthorizationId,
            AmountHeld = order.Payment?.AuthorizedAmount ?? 0m,
            Currency = order.Payment?.CurrencyCode ?? string.Empty
        };

        return Results.Ok(response);
    }
}
