using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.Extensions.Options;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Operator action: marks the order fulfilled — and that is when the money is actually taken
/// (captured). A stale hold is renewed first; one that can no longer be renewed reports so.
/// </summary>
public class FulfilOrderEndpoint : IEndpoint<IResult, int, IPaymentService>
{
    private readonly string _currency;

    public FulfilOrderEndpoint(IOptions<PaymentSettings> paymentSettings)
    {
        _currency = paymentSettings.Value.Currency;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId:int}/fulfil",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS,
                AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, IPaymentService paymentService) =>
            {
                return await HandleAsync(orderId, paymentService);
            })
            .Produces<OrderResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(int orderId, IPaymentService paymentService)
    {
        var result = await paymentService.FulfilAsync(orderId);
        return Results.Ok(OrderResponseMapper.ToResponse(result, _currency));
    }
}
