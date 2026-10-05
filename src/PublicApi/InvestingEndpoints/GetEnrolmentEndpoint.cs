using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.Extensions.DependencyInjection;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Reports where the caller's enrolment has got to.</summary>
public class GetEnrolmentEndpoint : IEndpoint<IResult, HttpContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (HttpContext http, CancellationToken _) => await HandleAsync(http))
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(HttpContext http)
    {
        var shopper = InvestingContractMapping.ResolveShopper(http);
        if (string.IsNullOrEmpty(shopper))
            return Results.Unauthorized();

        var investing = http.RequestServices.GetRequiredService<IInvestingService>();
        var enrolment = await investing.GetEnrolmentAsync(shopper, http.RequestAborted);
        if (enrolment is null)
            return Results.NotFound(new { error = "The caller is not enrolled." });

        return Results.Ok(new EnrolmentResponse
        {
            EnrolmentId = enrolment.EnrolmentId,
            Status = InvestingContractMapping.ToApi(enrolment.Status),
        });
    }
}
