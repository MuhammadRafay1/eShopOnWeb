using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Flow 1 — POST /api/portfolio/investor: sign the signed-in shopper up as an investor.</summary>
public class SignUpInvestorEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                SignUpInvestorRequest request,
                ClaimsPrincipal user,
                IInvestorService service,
                CancellationToken ct) =>
            {
                var userName = PortfolioEndpointHelpers.GetUserName(user);
                if (string.IsNullOrEmpty(userName)) return Results.Unauthorized();

                var errors = PortfolioEndpointHelpers.ValidateSignup(request);
                if (errors.Count > 0)
                    return Results.BadRequest(new { message = "Missing or invalid fields.", fields = errors });

                try
                {
                    var investor = await service.SignUpAsync(userName, PortfolioEndpointHelpers.ToSignup(request), ct);
                    return Results.Ok(ToResponse(investor));
                }
                catch (UpvestException ex)
                {
                    return PortfolioEndpointHelpers.ToResult(ex);
                }
            })
            .Produces<InvestorResponse>()
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .WithTags("PortfolioEndpoints");
    }

    internal static InvestorResponse ToResponse(Investor investor) => new()
    {
        InvestorId = investor.UpvestUserId ?? investor.Id.ToString(),
        Status = PortfolioStatusText.Of(investor.MapStatus())
    };
}

/// <summary>Flow 1 — GET /api/portfolio/investor: where the caller's sign-up has got to.</summary>
public class GetInvestorEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                ClaimsPrincipal user,
                IInvestorService service,
                CancellationToken ct) =>
            {
                var userName = PortfolioEndpointHelpers.GetUserName(user);
                if (string.IsNullOrEmpty(userName)) return Results.Unauthorized();

                var investor = await service.GetAsync(userName, ct);
                if (investor is null)
                    return Results.NotFound(new { message = "You have not signed up as an investor yet." });

                return Results.Ok(SignUpInvestorEndpoint.ToResponse(investor));
            })
            .Produces<InvestorResponse>()
            .WithTags("PortfolioEndpoints");
    }
}
