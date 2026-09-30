using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

public class ListPaymentMethodsRequest
{
    public string BuyerId { get; set; } = string.Empty;
}

public class PaymentMethodDto
{
    public int PaymentMethodId { get; set; }
    public string? Brand { get; set; }
    public string? Last4 { get; set; }
    public string? Expiry { get; set; }
    public string? Alias { get; set; }
}

public class ListPaymentMethodsResponse
{
    public List<PaymentMethodDto> PaymentMethods { get; set; } = new();
}

/// <summary>The caller's own saved cards - safe descriptors only, never full card details.</summary>
public class ListPaymentMethodsEndpoint : IEndpoint<IResult, ListPaymentMethodsRequest, PaymentMethodEndpointServices>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, PaymentMethodEndpointServices services) =>
            {
                return await HandleAsync(new ListPaymentMethodsRequest { BuyerId = user.Identity!.Name! }, services);
            })
            .Produces<ListPaymentMethodsResponse>()
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(ListPaymentMethodsRequest request, PaymentMethodEndpointServices services)
    {
        var buyer = await services.BuyerRepository.FirstOrDefaultAsync(new BuyerWithPaymentMethodsSpecification(request.BuyerId));

        var response = new ListPaymentMethodsResponse
        {
            PaymentMethods = buyer?.PaymentMethods.Select(p => new PaymentMethodDto
            {
                PaymentMethodId = p.Id,
                Brand = p.Brand,
                Last4 = p.Last4,
                Expiry = p.Expiry,
                Alias = p.Alias
            }).ToList() ?? new()
        };

        return Results.Ok(response);
    }
}
