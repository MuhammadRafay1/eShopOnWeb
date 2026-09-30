using System;
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

public class CancelOrderRequest : BaseRequest
{
    public CancelOrderRequest(int orderId) => OrderId = orderId;
    public int OrderId { get; set; }
}

public class CancelOrderResponse : BaseResponse
{
    public CancelOrderResponse(Guid correlationId) : base(correlationId) { }

    public int OrderId { get; set; }
    public string Status { get; set; } = "";
}

/// <summary>Operator action: cancels an order before fulfilment, releasing any held funds on PayPal.</summary>
public class CancelOrderEndpoint : IEndpoint<IResult, CancelOrderRequest, IOrderPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/cancel",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, IOrderPaymentService paymentService, CancellationToken ct) =>
            {
                return await HandleAsync(new CancelOrderRequest(orderId), paymentService, ct);
            })
            .Produces<CancelOrderResponse>()
            .WithTags("OrderPaymentEndpoints");
    }

    public Task<IResult> HandleAsync(CancelOrderRequest request, IOrderPaymentService paymentService) =>
        HandleAsync(request, paymentService, CancellationToken.None);

    public async Task<IResult> HandleAsync(CancelOrderRequest request, IOrderPaymentService paymentService, CancellationToken ct)
    {
        var result = await paymentService.CancelAsync(request.OrderId, ct);
        return Results.Ok(new CancelOrderResponse(request.CorrelationId())
        {
            OrderId = result.OrderId,
            Status = result.Status.ToString()
        });
    }
}
