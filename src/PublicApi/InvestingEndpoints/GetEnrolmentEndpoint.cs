using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Reports where the signed-in shopper's enrolment has got to.</summary>
public class GetEnrolmentEndpoint : IEndpoint<IResult, GetEnrolmentEndpoint.Dependencies>
{
    public record struct Dependencies(
        ClaimsPrincipal User, IInvestingService Investing, CancellationToken CancellationToken);

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                ClaimsPrincipal user,
                IInvestingService investing,
                CancellationToken cancellationToken) =>
            {
                return await HandleAsync(new Dependencies(user, investing, cancellationToken));
            })
            .Produces<EnrolmentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(Dependencies deps)
    {
        var shopperId = deps.User.Identity?.Name;
        if (string.IsNullOrEmpty(shopperId))
        {
            return Results.Unauthorized();
        }

        var investor = await deps.Investing.GetEnrolmentAsync(shopperId, deps.CancellationToken);
        if (investor is null)
        {
            return Results.NotFound("The shopper is not enrolled.");
        }

        return Results.Ok(new EnrolmentResponse
        {
            EnrolmentId = investor.EnrolmentId,
            Status = investor.Status.ToText()
        });
    }
}
