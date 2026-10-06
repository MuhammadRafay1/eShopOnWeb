using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Reports where the signed-in shopper's enrolment has got to.</summary>
public class EnrolmentGetEndpoint : IEndpoint<IResult, HttpContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (HttpContext context, System.Security.Claims.ClaimsPrincipal user) => await HandleAsync(context))
            .Produces<EnrolmentResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(HttpContext context)
    {
        var shopperId = context.User.Identity?.Name;
        if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();

        var investingService = context.RequestServices.GetRequiredService<IInvestingService>();
        var investor = await investingService.GetEnrolmentAsync(shopperId, CancellationToken.None);
        if (investor is null) return Results.NotFound();

        return Results.Ok(new EnrolmentResponse
        {
            EnrolmentId = investor.PublicId,
            Status = investor.Status.ToWire(),
        });
    }
}
