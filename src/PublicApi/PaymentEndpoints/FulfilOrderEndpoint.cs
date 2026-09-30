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

public class FulfilOrderRequest : BaseRequest
{
    public FulfilOrderRequest(int orderId) => OrderId = orderId;
    public int OrderId { get; set; }
}

public class FulfilOrderResponse : BaseResponse
{
    public FulfilOrderResponse(Guid correlationId) : base(correlationId) { }

    public int OrderId { get; set; }
    public string Status { get; set; } = "";
    public decimal CapturedAmount { get; set; }
    public decimal? PayPalFee { get; set; }
    public decimal? NetAmount { get; set; }
    public string Currency { get; set; } = "";
}

/// <summary>Operator action: fulfils the order, capturing the held funds (renewing a stale hold first if needed).</summary>
public class FulfilOrderEndpoint : IEndpoint<IResult, FulfilOrderRequest, IOrderPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/fulfil",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, IOrderPaymentService paymentService, CancellationToken ct) =>
            {
                return await HandleAsync(new FulfilOrderRequest(orderId), paymentService, ct);
            })
            .Produces<FulfilOrderResponse>()
            .WithTags("OrderPaymentEndpoints");
    }

    public Task<IResult> HandleAsync(FulfilOrderRequest request, IOrderPaymentService paymentService) =>
        HandleAsync(request, paymentService, CancellationToken.None);

    public async Task<IResult> HandleAsync(FulfilOrderRequest request, IOrderPaymentService paymentService, CancellationToken ct)
    {
        var result = await paymentService.FulfilAsync(request.OrderId, ct);
        return Results.Ok(new FulfilOrderResponse(request.CorrelationId())
        {
            OrderId = result.OrderId,
            Status = result.Status.ToString(),
            CapturedAmount = result.CapturedAmount,
            PayPalFee = result.PayPalFee,
            NetAmount = result.NetAmount,
            Currency = result.Currency
        });
    }
}
