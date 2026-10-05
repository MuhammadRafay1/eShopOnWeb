using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Where the caller's enrolment has got to.</summary>
public class GetEnrolmentEndpoint : IEndpoint<IResult, EmptyInvestingRequest, IInvestingService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetEnrolmentEndpoint(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IInvestingService investingService) =>
            {
                return await HandleAsync(new EmptyInvestingRequest(), investingService);
            })
            .Produces<EnrolInvestingResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(EmptyInvestingRequest request, IInvestingService investingService)
    {
        var buyerId = _httpContextAccessor.HttpContext?.User.GetBuyerId();
        if (string.IsNullOrEmpty(buyerId))
            return Results.Unauthorized();

        var result = await investingService.GetEnrolmentAsync(buyerId);
        if (result is null)
            return Results.NotFound();

        return Results.Ok(new EnrolInvestingResponse(request.CorrelationId())
        {
            EnrolmentId = result.EnrolmentId,
            Status = result.Status.ToText()
        });
    }
}
