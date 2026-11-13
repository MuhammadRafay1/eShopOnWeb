using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Flow 2 — lists every model portfolio and its funds. Available to any signed-in shopper.
/// </summary>
public class GetModelsEndpoint : PortfolioEndpointBase, IEndpoint<IResult>
{
    public GetModelsEndpoint(IHttpContextAccessor accessor) : base(accessor) { }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/models",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            () => await HandleAsync())
            .Produces<ModelsResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync()
    {
        var models = Service<IReadRepository<ModelPortfolio>>();
        var all = await models.ListAsync(new ModelPortfoliosAllSpecification(), Ct);
        return Results.Ok(new ModelsResponse(System.Guid.NewGuid())
        {
            Models = all.Select(m => new ModelDto
            {
                ModelId = m.ModelId,
                Name = m.Name,
                Funds = m.Funds.Select(f => new FundDto { InstrumentId = f.InstrumentId, Weight = f.Weight }).ToList()
            }).ToList()
        });
    }
}
