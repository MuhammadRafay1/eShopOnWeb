using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Reports where the signed-in shopper's enrolment has got to.</summary>
public class GetEnrolmentEndpoint : IEndpoint<IResult, IInvestingService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetEnrolmentEndpoint(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (IInvestingService investing) => await HandleAsync(investing))
            .Produces<EnrolmentResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(IInvestingService investing)
    {
        var buyerId = CallerIdentity.GetBuyerId(_httpContextAccessor);
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        var investor = await investing.GetEnrolmentAsync(buyerId);
        if (investor == null)
        {
            return Results.NotFound("This shopper has not opted in to investing.");
        }

        return Results.Ok(new EnrolmentResponse
        {
            EnrolmentId = investor.PublicId,
            Status = InvestingStatus.ToApi(investor.Status),
        });
    }
}
