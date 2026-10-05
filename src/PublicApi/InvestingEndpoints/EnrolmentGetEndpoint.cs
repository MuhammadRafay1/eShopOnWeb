using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.Extensions.DependencyInjection;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Where the caller's enrolment has got to (Flow 1).</summary>
public class EnrolmentGetEndpoint : IEndpoint<IResult, ClaimsPrincipal>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EnrolmentGetEndpoint(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ClaimsPrincipal user) => await HandleAsync(user))
            .Produces<EnrolmentResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(ClaimsPrincipal user)
    {
        var shopperId = user.ShopperId();
        if (string.IsNullOrEmpty(shopperId))
            return Results.Unauthorized();

        using var scope = _scopeFactory.CreateScope();
        var investing = scope.ServiceProvider.GetRequiredService<InvestingService>();

        var enrolment = await investing.GetEnrolmentAsync(shopperId, default);
        if (enrolment is null)
            return Results.NotFound(new { error = "Not enrolled." });

        return Results.Ok(new EnrolmentResponse { EnrolmentId = enrolment.PublicId, Status = enrolment.Status.ToWire() });
    }
}
