using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class RefundOrderRequest : BaseRequest
{
    public int OrderId { get; set; }
    public decimal? Amount { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}

public class RefundOrderResponse : BaseResponse
{
    public string RefundId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

/// <summary>Operator action: refunds a captured payment, in full or in part. Idempotent per caller-supplied key.</summary>
public class RefundOrderEndpoint : IEndpoint<IResult, RefundOrderRequest, IOrderPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/refunds",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, RefundOrderRequest request, IOrderPaymentService orderPaymentService) =>
            {
                request.OrderId = orderId;
                return await HandleAsync(request, orderPaymentService);
            })
            .Produces<RefundOrderResponse>()
            .WithTags("OrderPaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(RefundOrderRequest request, IOrderPaymentService orderPaymentService)
    {
        var outcome = await orderPaymentService.RefundAsync(request.OrderId, request.Amount, request.IdempotencyKey, default);

        var response = new RefundOrderResponse
        {
            RefundId = outcome.RefundId,
            Status = outcome.Status,
            Amount = outcome.Amount
        };
        return Results.Ok(response);
    }
}
