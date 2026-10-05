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

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Where the caller's enrolment has got to.</summary>
public class GetEnrolmentEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ClaimsPrincipal user, IInvestingService investing, CancellationToken cancellationToken) =>
            {
                var shopperId = user.Identity?.Name;
                if (string.IsNullOrEmpty(shopperId))
                    return Results.Unauthorized();

                var investor = await investing.GetInvestorAsync(shopperId, cancellationToken);
                return investor is null
                    ? Results.NotFound()
                    : Results.Ok(EnrolmentResponse.From(investor));
            })
            .Produces<EnrolmentResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("InvestingEndpoints");
    }
}
