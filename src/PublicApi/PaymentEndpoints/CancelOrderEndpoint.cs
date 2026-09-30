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

public class CancelOrderRequest
{
    public int OrderId { get; set; }
    public CancelOrderRequest(int orderId) => OrderId = orderId;
    public CancelOrderRequest() { }
}

public class CancelOrderResponse
{
    public int OrderId { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public PaymentView Payment { get; set; } = new();
}

/// <summary>
/// Operator action: cancels an authorized-but-unfulfilled order, voiding the hold so no money ever
/// moves. Restricted to administrators.
/// </summary>
public class CancelOrderEndpoint : IEndpoint<IResult, CancelOrderRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId:int}/cancel",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, IPaymentService paymentService, CancellationToken ct) =>
            {
                return await HandleAsync(new CancelOrderRequest(orderId), paymentService, ct);
            })
            .Produces<CancelOrderResponse>()
            .WithTags("Orders");
    }

    public Task<IResult> HandleAsync(CancelOrderRequest request, IPaymentService paymentService)
        => HandleAsync(request, paymentService, CancellationToken.None);

    public async Task<IResult> HandleAsync(CancelOrderRequest request, IPaymentService paymentService, CancellationToken ct)
    {
        var payment = await paymentService.CancelOrderAsync(request.OrderId, ct);
        return Results.Ok(new CancelOrderResponse
        {
            OrderId = request.OrderId,
            OrderStatus = "Cancelled",
            Payment = PaymentView.From(payment)
        });
    }
}
