using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Where the signed-in shopper's enrolment has got to (Flow 1).</summary>
public class EnrolmentGetEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                HttpContext http, IInvestingService investing) =>
            {
                var shopperId = http.User.Identity?.Name;
                if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();

                try
                {
                    var investor = await investing.GetEnrolmentAsync(shopperId, http.RequestAborted);
                    if (investor is null) return Results.NotFound(new { error = "You are not enrolled in investing." });

                    return Results.Ok(new EnrolmentResponse
                    {
                        EnrolmentId = investor.Id,
                        Status = InvestingHttp.ToWire(investor.Status)
                    });
                }
                catch (UpvestIntegrationException ex)
                {
                    return InvestingHttp.Problem(ex);
                }
            })
            .Produces<EnrolmentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags(InvestingHttp.Tag);
    }
}
