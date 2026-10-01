using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
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

/// <summary>GET /api/payment-methods — the caller's own saved cards. (any authenticated shopper)</summary>
public class GetPaymentMethodsEndpoint : IEndpoint<IResult, GetPaymentMethodsRequest, ISavedCardService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ISavedCardService service, ClaimsPrincipal user, CancellationToken ct) =>
                await HandleAsync(new GetPaymentMethodsRequest(user.GetBuyerId()), service, ct))
            .Produces<List<PaymentMethodDto>>()
            .WithTags("PaymentMethodEndpoints");
    }

    public Task<IResult> HandleAsync(GetPaymentMethodsRequest request, ISavedCardService service) =>
        HandleAsync(request, service, default);

    public async Task<IResult> HandleAsync(GetPaymentMethodsRequest request, ISavedCardService service, CancellationToken ct)
    {
        var methods = await service.ListAsync(request.BuyerId, ct);
        var result = methods.Select(m => m.ToDto()).ToList();
        return Results.Ok(result);
    }
}

public record GetPaymentMethodsRequest(string BuyerId);
