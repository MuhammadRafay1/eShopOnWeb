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

public class RefundOrderRequestBody
{
    public decimal? Amount { get; init; }
    public string IdempotencyKey { get; init; } = string.Empty;
}

public class RefundOrderCommand
{
    public int OrderId { get; init; }
    public decimal? Amount { get; init; }
    public string IdempotencyKey { get; init; } = string.Empty;
}

public class RefundOrderResponse
{
    public string RefundId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal TotalRefunded { get; init; }
}

/// <summary>
/// Refunds a captured order, in full or in part. Repeating the same idempotencyKey returns the
/// original refund rather than refunding twice; distinct keys allow legitimate successive partial
/// refunds. A refund can never exceed what was actually captured.
/// </summary>
public class RefundOrderEndpoint : IEndpoint<IResult, RefundOrderCommand, string, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId:int}/refunds",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (int orderId, RefundOrderRequestBody body, ClaimsPrincipal user, IPaymentService paymentService) =>
            {
                var command = new RefundOrderCommand { OrderId = orderId, Amount = body.Amount, IdempotencyKey = body.IdempotencyKey };
                return await HandleAsync(command, user.Identity!.Name!, paymentService);
            })
            .Produces<RefundOrderResponse>(StatusCodes.Status201Created)
            .WithTags("OrderPaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(RefundOrderCommand request, string buyerId, IPaymentService paymentService)
    {
        var outcome = await paymentService.RefundOrderAsync(request.OrderId, buyerId, request.Amount, request.IdempotencyKey);
        var response = new RefundOrderResponse
        {
            RefundId = outcome.RefundId,
            Status = outcome.Status,
            Amount = outcome.Amount,
            TotalRefunded = outcome.TotalRefunded
        };
        return Results.Created($"api/orders/{request.OrderId}/refunds/{response.RefundId}", response);
    }
}
