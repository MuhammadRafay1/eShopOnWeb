using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.Extensions.Options;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Authorizes (holds) the order total. The money is held, not taken. Pays with either one-off card
/// details or one of the shopper's saved cards. Owner-scoped: a non-owner gets 404.
/// </summary>
public class PayOrderEndpoint : IEndpoint<IResult, PayOrderRequest, IPaymentService>
{
    private readonly string _currency;

    public PayOrderEndpoint(IOptions<PaymentSettings> paymentSettings)
    {
        _currency = paymentSettings.Value.Currency;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId:int}/pay",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, PayOrderRequest request, ClaimsPrincipal user, IPaymentService paymentService) =>
            {
                request.OrderId = orderId;
                request.BuyerId = CallerIdentity.GetBuyerId(user);
                return await HandleAsync(request, paymentService);
            })
            .Produces<OrderResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(PayOrderRequest request, IPaymentService paymentService)
    {
        var card = request.Card is null ? null : OrderResponseMapper.ToCardDetails(request.Card);
        var result = await paymentService.PayAsync(request.OrderId, request.BuyerId, card, request.PaymentMethodId);
        return Results.Ok(OrderResponseMapper.ToResponse(result, _currency));
    }
}
