using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Flow 1 — where the caller's enrolment has got to.</summary>
public class GetEnrolmentEndpoint : IEndpoint<IResult>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ClaimsPrincipal user, IInvestingService service, CancellationToken ct) =>
            {
                var buyerId = InvestingMapping.BuyerId(user);
                if (string.IsNullOrWhiteSpace(buyerId)) return Results.Unauthorized();

                var view = await service.GetEnrolmentAsync(buyerId, ct);
                if (view is null) return Results.NotFound(new { message = "Not enrolled." });

                return Results.Ok(new EnrolmentResponse
                {
                    EnrolmentId = view.EnrolmentId,
                    Status = InvestingMapping.ToWire(view.Status),
                });
            })
            .Produces<EnrolmentResponse>()
            .WithTags("InvestingEndpoints");
    }

    public Task<IResult> HandleAsync() => Task.FromResult<IResult>(Results.Empty);
}
