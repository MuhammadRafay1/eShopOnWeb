using System.Linq;
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

/// <summary>The caller's investments, newest first (Flow 3/4).</summary>
public class InvestmentsGetEndpoint : IEndpoint<IResult, ClaimsPrincipal>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public InvestmentsGetEndpoint(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/investments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ClaimsPrincipal user) => await HandleAsync(user))
            .Produces<InvestmentsResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(ClaimsPrincipal user)
    {
        var shopperId = user.ShopperId();
        if (string.IsNullOrEmpty(shopperId))
            return Results.Unauthorized();

        using var scope = _scopeFactory.CreateScope();
        var investing = scope.ServiceProvider.GetRequiredService<InvestingService>();

        var investments = await investing.ListInvestmentsAsync(shopperId, default);
        var items = investments.Select(i => new InvestmentItem
        {
            InvestmentId = i.Id,
            Amount = InvestingMappings.ToEuros(i.AmountCents),
            Status = i.Status.ToWire(),
        }).ToList();

        return Results.Ok(new InvestmentsResponse { Investments = items });
    }
}
