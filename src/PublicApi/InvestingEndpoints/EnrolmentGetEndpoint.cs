using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Shows where the signed-in shopper's enrolment has got to.
/// </summary>
public class EnrolmentGetEndpoint : IEndpoint<IResult, IEnrolmentService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EnrolmentGetEndpoint(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IEnrolmentService enrolmentService) =>
            {
                return await HandleAsync(enrolmentService);
            })
            .Produces<EnrolmentResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(IEnrolmentService enrolmentService)
    {
        var httpContext = _httpContextAccessor.HttpContext!;
        var ct = httpContext.RequestAborted;
        var buyerId = httpContext.User.Identity?.Name;
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        var investor = await enrolmentService.GetEnrolmentAsync(buyerId, ct);
        if (investor is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new EnrolmentResponse
        {
            EnrolmentId = investor.Id,
            Status = investor.Status.ToString().ToLowerInvariant()
        });
    }
}
