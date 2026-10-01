using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

public class PaymentMethodDto
{
    public string PaymentMethodId { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string LastDigits { get; set; } = string.Empty;
    public string? Expiry { get; set; }
    public string? CardholderName { get; set; }
}

public class ListPaymentMethodsResponse : BaseResponse
{
    public List<PaymentMethodDto> PaymentMethods { get; set; } = new();
}

/// <summary>The caller's own saved cards.</summary>
public class ListPaymentMethodsEndpoint : IEndpoint<IResult, string, IPaymentMethodService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IPaymentMethodService paymentMethodService) =>
            {
                var buyerId = user.FindFirstValue(ClaimTypes.Name)!;
                return await HandleAsync(buyerId, paymentMethodService);
            })
            .Produces<ListPaymentMethodsResponse>()
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(string buyerId, IPaymentMethodService paymentMethodService)
    {
        var methods = await paymentMethodService.ListAsync(buyerId, default);
        var response = new ListPaymentMethodsResponse
        {
            PaymentMethods = methods.Select(m => new PaymentMethodDto
            {
                PaymentMethodId = m.PaymentMethodId,
                Brand = m.Brand,
                LastDigits = m.LastDigits,
                Expiry = m.Expiry,
                CardholderName = m.CardholderName
            }).ToList()
        };
        return Results.Ok(response);
    }
}
