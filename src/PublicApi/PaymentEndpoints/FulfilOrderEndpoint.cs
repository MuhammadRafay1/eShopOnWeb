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

public class FulfilOrderRequest
{
    public int OrderId { get; set; }
    public FulfilOrderRequest(int orderId) => OrderId = orderId;
    public FulfilOrderRequest() { }
}

public class FulfilOrderResponse
{
    public int OrderId { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public PaymentView Payment { get; set; } = new();
}

/// <summary>
/// Operator action: marks the order fulfilled and captures (takes) the held funds. A stale
/// authorization is renewed before capture; one that can no longer be renewed yields an
/// operator-actionable conflict. Restricted to administrators.
/// </summary>
public class FulfilOrderEndpoint : IEndpoint<IResult, FulfilOrderRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId:int}/fulfil",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, IPaymentService paymentService, CancellationToken ct) =>
            {
                return await HandleAsync(new FulfilOrderRequest(orderId), paymentService, ct);
            })
            .Produces<FulfilOrderResponse>()
            .WithTags("Orders");
    }

    public Task<IResult> HandleAsync(FulfilOrderRequest request, IPaymentService paymentService)
        => HandleAsync(request, paymentService, CancellationToken.None);

    public async Task<IResult> HandleAsync(FulfilOrderRequest request, IPaymentService paymentService, CancellationToken ct)
    {
        var payment = await paymentService.FulfilOrderAsync(request.OrderId, ct);
        return Results.Ok(new FulfilOrderResponse
        {
            OrderId = request.OrderId,
            OrderStatus = "Fulfilled",
            Payment = PaymentView.From(payment)
        });
    }
}
