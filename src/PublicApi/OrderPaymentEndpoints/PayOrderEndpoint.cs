using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class PayOrderRequestBody
{
    public CardRequestDto? Card { get; init; }
    public int? SavedCardId { get; init; }
}

public class PayOrderCommand
{
    public int OrderId { get; init; }
    public CardRequestDto? Card { get; init; }
    public int? SavedCardId { get; init; }
}

public class PaymentActionResponse
{
    public int OrderId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? AuthorizationId { get; init; }
    public string? CaptureId { get; init; }
    public decimal? CapturedAmount { get; init; }
    public decimal? PayPalFee { get; init; }
    public decimal? NetAmount { get; init; }
    public RefundDto[] Refunds { get; init; } = System.Array.Empty<RefundDto>();

    public static PaymentActionResponse From(PaymentActionResult r) => new()
    {
        OrderId = r.OrderId,
        Status = r.Status,
        Currency = r.Currency,
        Amount = r.Amount,
        AuthorizationId = r.AuthorizationId,
        CaptureId = r.CaptureId,
        CapturedAmount = r.CapturedAmount,
        PayPalFee = r.PayPalFee,
        NetAmount = r.NetAmount,
        Refunds = r.Refunds.Select(x => new RefundDto { RefundId = x.RefundId, Amount = x.Amount, Status = x.Status }).ToArray()
    };
}

public class RefundDto
{
    public string RefundId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Status { get; init; } = string.Empty;
}

/// <summary>
/// Authorizes (holds) an order's total with a one-off card or a saved card. Does not take the
/// money - fulfilment does that. A repeat call while already authorized (or beyond) is a no-op.
/// </summary>
public class PayOrderEndpoint : IEndpoint<IResult, PayOrderCommand, string, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId:int}/pay",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (int orderId, PayOrderRequestBody body, ClaimsPrincipal user, IPaymentService paymentService) =>
            {
                var command = new PayOrderCommand { OrderId = orderId, Card = body.Card, SavedCardId = body.SavedCardId };
                return await HandleAsync(command, user.Identity!.Name!, paymentService);
            })
            .Produces<PaymentActionResponse>()
            .WithTags("OrderPaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(PayOrderCommand request, string buyerId, IPaymentService paymentService)
    {
        if ((request.Card is null) == (request.SavedCardId is null))
        {
            return Results.BadRequest(new { message = "Provide exactly one of card or savedCardId." });
        }

        var cardDetails = request.Card?.ToCardDetails();
        var result = await paymentService.AuthorizePaymentAsync(request.OrderId, buyerId, cardDetails, request.SavedCardId);
        return Results.Ok(PaymentActionResponse.From(result));
    }
}
