using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Where the caller's enrolment has got to (Flow 1).</summary>
public class GetEnrolmentEndpoint : IEndpoint<IResult, IInvestingService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetEnrolmentEndpoint(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (IInvestingService investingService) => await HandleAsync(investingService))
            .Produces<EnrolmentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(IInvestingService investingService)
    {
        var buyerId = CallerId.Resolve(_httpContextAccessor.HttpContext?.User);
        if (string.IsNullOrEmpty(buyerId))
            return Results.Unauthorized();

        var view = await investingService.GetEnrolmentAsync(buyerId, _httpContextAccessor.HttpContext!.RequestAborted);
        return view is null
            ? Results.NotFound(new { error = "Not enrolled." })
            : Results.Ok(new EnrolmentResponse { EnrolmentId = view.EnrolmentId, Status = view.Status });
    }
}
