using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// GET /api/investing/enrolment — where the caller's enrolment has got to.
/// </summary>
public class GetEnrolmentEndpoint : IEndpoint<IResult, HttpContext, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (HttpContext http, IInvestingService service) => await HandleAsync(http, service))
            .Produces<EnrolmentResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(HttpContext http, IInvestingService service)
    {
        var buyerId = InvestingEndpointHelpers.BuyerId(http.User);
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        var view = await service.GetEnrolmentAsync(buyerId, http.RequestAborted);
        if (view is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new EnrolmentResponse { EnrolmentId = view.EnrolmentId, Status = view.Status.Wire() });
    }
}
