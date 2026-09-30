using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

public class ListPaymentMethodsRequest : BaseRequest
{
    [JsonIgnore] public string BuyerId { get; set; } = "";
}

public class PaymentMethodDto
{
    public int PaymentMethodId { get; set; }
    public string Brand { get; set; } = "";
    public string LastDigits { get; set; } = "";
    public string Expiry { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public class ListPaymentMethodsResponse : BaseResponse
{
    public ListPaymentMethodsResponse(Guid correlationId) : base(correlationId) { }
    public ListPaymentMethodsResponse() { }

    public List<PaymentMethodDto> PaymentMethods { get; set; } = new();
}

/// <summary>GET api/payment-methods — the caller's saved cards.</summary>
public class ListPaymentMethodsEndpoint : IEndpoint<IResult, ListPaymentMethodsRequest, IRepository<PaymentMethod>>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IRepository<PaymentMethod> repository) =>
            {
                return await HandleAsync(new ListPaymentMethodsRequest { BuyerId = user.Identity!.Name! }, repository);
            })
            .Produces<ListPaymentMethodsResponse>()
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(ListPaymentMethodsRequest request, IRepository<PaymentMethod> repository)
    {
        var response = new ListPaymentMethodsResponse(request.CorrelationId());

        var methods = await repository.ListAsync(new PaymentMethodsByBuyerSpecification(request.BuyerId));
        response.PaymentMethods = methods
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new PaymentMethodDto
            {
                PaymentMethodId = m.Id,
                Brand = m.Brand,
                LastDigits = m.LastDigits,
                Expiry = m.Expiry,
                CreatedAt = m.CreatedAt
            }).ToList();

        return Results.Ok(response);
    }
}
