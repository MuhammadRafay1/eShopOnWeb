using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>GET /api/investing/enrolment — where the signed-in shopper's enrolment has got to.</summary>
public class EnrolmentGetEndpoint : IEndpoint<IResult, HttpContext, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (HttpContext http, IInvestingService investing) => await HandleAsync(http, investing))
            .Produces<EnrolmentResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(HttpContext http, IInvestingService investing)
    {
        var buyerId = http.User.Identity?.Name;
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        var investor = await investing.GetEnrolmentAsync(buyerId, http.RequestAborted);
        if (investor is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new EnrolmentResponse
        {
            EnrolmentId = investor.EnrolmentId,
            Status = investor.Status.ToText()
        });
    }
}
