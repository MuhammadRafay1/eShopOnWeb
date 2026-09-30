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

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

public class PayOrderRequest : BaseRequest
{
    /// <summary>A one-off card. Provide this OR <see cref="PaymentMethodId"/>, not both.</summary>
    public CardDto? Card { get; set; }

    /// <summary>A saved card id to pay with. Provide this OR <see cref="Card"/>, not both.</summary>
    public int? PaymentMethodId { get; set; }

    public int OrderId { get; set; }
    public string BuyerId { get; set; } = "";
}

public class PayOrderResponse : BaseResponse
{
    public PayOrderResponse(Guid correlationId) : base(correlationId) { }

    public int OrderId { get; set; }
    public string Status { get; set; } = "";
    public decimal AuthorizedAmount { get; set; }
    public string Currency { get; set; } = "";
    public DateTimeOffset AuthorizationExpiresAt { get; set; }
}

/// <summary>Authorizes (holds) the order total against a one-off card or a saved card. Does not capture.</summary>
public class PayOrderEndpoint : IEndpoint<IResult, PayOrderRequest, IOrderPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/pay",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, PayOrderRequest request, ClaimsPrincipal user, IOrderPaymentService paymentService,
             CancellationToken ct) =>
            {
                request.OrderId = orderId;
                request.BuyerId = user.Identity!.Name!;
                return await HandleAsync(request, paymentService, ct);
            })
            .Produces<PayOrderResponse>()
            .WithTags("OrderPaymentEndpoints");
    }

    public Task<IResult> HandleAsync(PayOrderRequest request, IOrderPaymentService paymentService) =>
        HandleAsync(request, paymentService, CancellationToken.None);

    public async Task<IResult> HandleAsync(PayOrderRequest request, IOrderPaymentService paymentService, CancellationToken ct)
    {
        var result = await paymentService.AuthorizeAsync(
            request.OrderId, request.BuyerId, request.Card?.ToCardDetails(), request.PaymentMethodId, ct);

        return Results.Ok(new PayOrderResponse(request.CorrelationId())
        {
            OrderId = result.OrderId,
            Status = result.Status.ToString(),
            AuthorizedAmount = result.AuthorizedAmount,
            Currency = result.Currency,
            AuthorizationExpiresAt = result.AuthorizationExpiresAt
        });
    }
}
