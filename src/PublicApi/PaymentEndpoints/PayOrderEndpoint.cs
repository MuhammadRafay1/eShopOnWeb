using System.Security.Claims;
using System.Text.Json.Serialization;
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

public class PayOrderRequest
{
    /// <summary>Card details for a one-off payment. Mutually exclusive with <see cref="PaymentMethodId"/>.</summary>
    public CardModel? Card { get; set; }

    /// <summary>Id of one of the shopper's saved cards. Mutually exclusive with <see cref="Card"/>.</summary>
    public int? PaymentMethodId { get; set; }

    [JsonIgnore] public int OrderId { get; set; }
    [JsonIgnore] public string BuyerId { get; set; } = string.Empty;
}

public class PayOrderResponse
{
    public int OrderId { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public PaymentView Payment { get; set; } = new();
}

/// <summary>
/// Authorizes (holds) the order total with PayPal — funds are held, not taken. Pays with raw card
/// details or one of the shopper's saved cards. Shopper-scoped: acts only on the caller's own order.
/// </summary>
public class PayOrderEndpoint : IEndpoint<IResult, PayOrderRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId:int}/pay",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, PayOrderRequest request, ClaimsPrincipal user, IPaymentService paymentService, CancellationToken ct) =>
            {
                request.OrderId = orderId;
                request.BuyerId = user.Identity!.Name!;
                return await HandleAsync(request, paymentService, ct);
            })
            .Produces<PayOrderResponse>()
            .WithTags("Orders");
    }

    public Task<IResult> HandleAsync(PayOrderRequest request, IPaymentService paymentService)
        => HandleAsync(request, paymentService, CancellationToken.None);

    public async Task<IResult> HandleAsync(PayOrderRequest request, IPaymentService paymentService, CancellationToken ct)
    {
        var payment = await paymentService.AuthorizeOrderAsync(
            request.OrderId, request.BuyerId, request.Card?.ToPaymentCard(), request.PaymentMethodId, ct);

        return Results.Ok(new PayOrderResponse
        {
            OrderId = request.OrderId,
            OrderStatus = "PaymentAuthorized",
            Payment = PaymentView.From(payment)
        });
    }
}
