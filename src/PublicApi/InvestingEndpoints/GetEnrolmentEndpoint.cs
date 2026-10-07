using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Where the signed-in shopper's enrolment has got to (GET api/investing/enrolment).</summary>
public class GetEnrolmentEndpoint : IEndpoint<IResult, string, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IInvestingService investingService) =>
            {
                var buyerId = user.FindFirstValue(ClaimTypes.Name);
                return string.IsNullOrEmpty(buyerId) ? Results.Unauthorized() : await HandleAsync(buyerId, investingService);
            })
            .Produces<EnrolmentResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(string buyerId, IInvestingService investingService)
    {
        var enrolment = await investingService.GetEnrolmentAsync(buyerId);
        if (enrolment is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new EnrolmentResponse
        {
            EnrolmentId = enrolment.EnrolmentId,
            Status = InvestingStatusText.Wire(enrolment.Status),
        });
    }
}
