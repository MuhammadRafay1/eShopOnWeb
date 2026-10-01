using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

/// <summary>
/// POST /api/orders/{orderId}/pay — authorize (hold) the order total. Carries one-off card details OR a saved
/// paymentMethodId. (the caller must own the order)
/// </summary>
public class PayOrderEndpoint : IEndpoint<IResult, PayOrderRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId:int}/pay",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, PayOrderRequest request, IPaymentService service, ClaimsPrincipal user, CancellationToken ct) =>
            {
                request.OrderId = orderId;
                request.CallerId = user.GetBuyerId();
                return await HandleAsync(request, service, ct);
            })
            .Produces<OrderPaymentDto>()
            .WithTags("OrderEndpoints");
    }

    public Task<IResult> HandleAsync(PayOrderRequest request, IPaymentService service) =>
        HandleAsync(request, service, default);

    public async Task<IResult> HandleAsync(PayOrderRequest request, IPaymentService service, CancellationToken ct)
    {
        if (request.Card is null && request.PaymentMethodId is null)
        {
            throw new BadPaymentRequestException("Supply card details or a saved paymentMethodId to pay.");
        }

        var order = await service.AuthorizeAsync(
            request.OrderId,
            request.CallerId,
            request.Card?.ToCardDetails(),
            request.PaymentMethodId,
            ct);

        return Results.Ok(order.ToDto());
    }
}

public class PayOrderRequest
{
    /// <summary>One-off card to charge. Mutually exclusive with <see cref="PaymentMethodId"/>.</summary>
    public CardInput? Card { get; set; }

    /// <summary>A saved card of the caller's to charge. Mutually exclusive with <see cref="Card"/>.</summary>
    public int? PaymentMethodId { get; set; }

    public int OrderId { get; set; }
    public string CallerId { get; set; } = string.Empty;
}
