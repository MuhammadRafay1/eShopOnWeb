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

/// <summary>Where the signed-in shopper's enrolment has got to.</summary>
public class GetEnrolmentEndpoint : IEndpoint<IResult, IInvestingService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetEnrolmentEndpoint(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IInvestingService service) => await HandleAsync(service))
            .Produces<EnrolmentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(IInvestingService service)
    {
        var httpContext = _httpContextAccessor.HttpContext!;
        var buyerId = httpContext.User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrEmpty(buyerId)) return Results.Unauthorized();

        var view = await service.GetEnrolmentAsync(buyerId, httpContext.RequestAborted);
        if (view is null) return Results.NotFound(new { error = "Not enrolled." });

        return Results.Ok(new EnrolmentResponse
        {
            EnrolmentId = view.EnrolmentId,
            Status = InvestingStatusText.ToWire(view.Status)
        });
    }
}
