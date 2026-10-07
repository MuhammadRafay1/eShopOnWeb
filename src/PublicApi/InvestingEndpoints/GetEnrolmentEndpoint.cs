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

/// <summary>Reports where the caller's enrolment has got to.</summary>
public class GetEnrolmentEndpoint : IEndpoint<IResult, CallerRequest, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IInvestingService investing) =>
            {
                var request = new CallerRequest { BuyerId = user.FindFirstValue(ClaimTypes.Name) ?? string.Empty };
                return await HandleAsync(request, investing);
            })
            .Produces<EnrolmentResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(CallerRequest request, IInvestingService investing)
    {
        if (string.IsNullOrEmpty(request.BuyerId))
        {
            return Results.Unauthorized();
        }

        var investor = await investing.GetEnrolmentAsync(request.BuyerId);
        if (investor is null)
        {
            return Results.NotFound();
        }

        var response = new EnrolmentResponse(request.CorrelationId())
        {
            EnrolmentId = investor.Id,
            Status = investor.Status.ToApiString()
        };
        return Results.Ok(response);
    }
}
