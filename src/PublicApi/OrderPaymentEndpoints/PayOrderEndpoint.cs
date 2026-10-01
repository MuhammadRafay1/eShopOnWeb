using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class PayOrderRequest : BaseRequest
{
    public int OrderId { get; set; }
    public CardDetailsDto? Card { get; set; }
    public string? SavedPaymentMethodId { get; set; }
}

/// <summary>Authorizes (holds, does not capture) the order total with a one-off card or a saved card.</summary>
public class PayOrderEndpoint : IEndpoint<IResult, PayOrderRequest, string, IOrderPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/pay",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, PayOrderRequest request, ClaimsPrincipal user, IOrderPaymentService orderPaymentService) =>
            {
                request.OrderId = orderId;
                var buyerId = user.FindFirstValue(ClaimTypes.Name)!;
                return await HandleAsync(request, buyerId, orderPaymentService);
            })
            .Produces<OrderStateDto>()
            .WithTags("OrderPaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(PayOrderRequest request, string buyerId, IOrderPaymentService orderPaymentService)
    {
        ApplicationCore.Interfaces.Payments.CardDetails? card = request.Card is null
            ? null
            : new ApplicationCore.Interfaces.Payments.CardDetails(
                request.Card.Number, request.Card.Expiry, request.Card.SecurityCode, request.Card.CardholderName,
                request.Card.Street, request.Card.City, request.Card.State, request.Card.Country, request.Card.PostalCode);

        var view = await orderPaymentService.PayAsync(request.OrderId, buyerId,
            new PayWithRequest(card, request.SavedPaymentMethodId), default);

        return Results.Ok(OrderStateMapper.ToDto(view));
    }
}
