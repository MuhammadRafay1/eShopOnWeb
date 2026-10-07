using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>GET /api/investing/enrolment — where the caller's enrolment has got to.</summary>
public class GetEnrolmentEndpoint : IEndpoint<IResult, ShopperScopedRequest, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IInvestingService service, HttpContext http) =>
            {
                var shopperId = InvestingShared.ShopperId(http.User);
                if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();
                return await HandleAsync(new ShopperScopedRequest(shopperId), service);
            })
            .Produces<EnrolmentResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(ShopperScopedRequest request, IInvestingService service)
    {
        var enrolment = await service.GetEnrolmentAsync(request.ShopperId);
        if (enrolment is null) return Results.NotFound();
        return Results.Ok(new EnrolmentResponse(enrolment.EnrolmentId, enrolment.Status.ToText()));
    }
}
