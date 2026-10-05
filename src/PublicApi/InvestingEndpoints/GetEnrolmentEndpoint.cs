using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;
using Microsoft.Extensions.DependencyInjection;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Where the caller's enrolment has got to (GET /api/investing/enrolment).</summary>
public class GetEnrolmentEndpoint : IEndpoint<IResult, HttpContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (HttpContext http, CancellationToken cancellationToken) => await HandleAsync(http))
            .Produces<EnrolmentResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(HttpContext http)
    {
        var shopperId = http.User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();

        var service = http.RequestServices.GetRequiredService<IInvestingService>();
        var enrolment = await service.GetEnrolmentAsync(shopperId, http.RequestAborted);
        if (enrolment is null) return Results.NotFound(new { error = "The shopper is not enrolled." });

        return Results.Ok(new EnrolmentResponse
        {
            EnrolmentId = enrolment.Id,
            Status = InvestingStatusText.For(enrolment.Status),
        });
    }
}
