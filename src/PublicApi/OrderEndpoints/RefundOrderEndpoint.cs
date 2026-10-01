using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.PublicApi.PaymentModels;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// POST api/orders/{orderId}/refunds — operator refunds the captured payment, in full or in part. The
/// caller-supplied idempotency key makes a repeat under the same key return the same refund; two distinct
/// keys are two legitimate partial refunds. Returns the new refund's id as a top-level field.
/// Administrator role only.
/// </summary>
public class RefundOrderEndpoint : IEndpoint<IResult, RefundOrderRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/refunds",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (int orderId, RefundOrderRequest request, IPaymentService service, CancellationToken ct) =>
            {
                request.OrderId = orderId;
                return await Run(request, service, ct);
            })
            .Produces(StatusCodes.Status200OK)
            .WithTags("OrderEndpoints");
    }

    public Task<IResult> HandleAsync(RefundOrderRequest request, IPaymentService service) =>
        Run(request, service, CancellationToken.None);

    private static async Task<IResult> Run(RefundOrderRequest request, IPaymentService service, CancellationToken ct)
    {
        var result = await service.RefundAsync(request.OrderId, request.Amount, request.IdempotencyKey, ct);
        return Results.Ok(new { refundId = result.RefundId, order = result.Order });
    }
}
