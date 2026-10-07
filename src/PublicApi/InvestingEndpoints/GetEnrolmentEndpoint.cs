using System.Security.Claims;
using System.Threading;
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
public class GetEnrolmentEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ClaimsPrincipal user, IInvestingService investingService, CancellationToken ct) =>
            {
                var buyerId = Caller.BuyerId(user);
                if (string.IsNullOrEmpty(buyerId))
                {
                    return Results.Unauthorized();
                }

                var view = await investingService.GetEnrolmentAsync(buyerId, ct);
                return view is null
                    ? Results.NotFound()
                    : Results.Ok(new EnrolmentResponse { EnrolmentId = view.EnrolmentId, Status = view.Status });
            })
            .Produces<EnrolmentResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("InvestingEndpoints");
    }
}
