using System.Linq;
using System.Security.Claims;
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

/// <summary>The signed-in shopper's own orders with their payment state.</summary>
public class MyOrdersEndpoint : IEndpoint<IResult, string, IPaymentService>
{
    private readonly string _currency;

    public MyOrdersEndpoint(IOptions<PaymentSettings> paymentSettings)
    {
        _currency = paymentSettings.Value.Currency;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/my-orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IPaymentService paymentService) =>
            {
                return await HandleAsync(CallerIdentity.GetBuyerId(user), paymentService);
            })
            .Produces<MyOrdersResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(string buyerId, IPaymentService paymentService)
    {
        var results = await paymentService.GetMyOrdersAsync(buyerId);
        var response = new MyOrdersResponse
        {
            Orders = results.Select(r => OrderResponseMapper.ToResponse(r, _currency)).ToList(),
        };
        return Results.Ok(response);
    }
}
