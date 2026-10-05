using System.Security.Claims;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.PublicApi.Investing;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Reports where the signed-in shopper's enrolment has got to (Flow 1).</summary>
public class GetEnrolmentEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                ClaimsPrincipal user,
                IRepository<InvestingAccount> repository) =>
                await HandleAsync(user, repository))
            .Produces<EnrolmentResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("InvestingEndpoints");
    }

    private static async Task<IResult> HandleAsync(ClaimsPrincipal user, IRepository<InvestingAccount> repository)
    {
        var buyerId = user.Identity?.Name;
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));

        var account = await repository.FirstOrDefaultAsync(new InvestingAccountByBuyerSpec(buyerId!));
        if (account is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new EnrolmentResponse
        {
            EnrolmentId = account.EnrolmentId,
            Status = account.Status.ToText()
        });
    }
}
