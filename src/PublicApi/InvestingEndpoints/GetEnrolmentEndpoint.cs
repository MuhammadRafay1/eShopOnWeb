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
public class GetEnrolmentEndpoint : IEndpoint<IResult, InvestingQueryRequest, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IInvestingService investing, HttpContext httpContext) =>
            {
                var request = new InvestingQueryRequest { BuyerId = httpContext.User.Identity?.Name ?? string.Empty };
                return await HandleAsync(request, investing);
            })
            .Produces<EnrolmentResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(InvestingQueryRequest request, IInvestingService investing)
    {
        if (string.IsNullOrEmpty(request.BuyerId))
        {
            return Results.Unauthorized();
        }

        var investor = await investing.GetInvestorAsync(request.BuyerId, default);
        if (investor is null)
        {
            return Results.NotFound("You have not opted in to investing your change.");
        }

        var response = new EnrolmentResponse(request.CorrelationId())
        {
            EnrolmentId = investor.Id,
            Status = InvestingStatusText.For(investor.Status)
        };
        return Results.Ok(response);
    }
}
