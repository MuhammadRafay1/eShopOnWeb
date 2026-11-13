using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Flow 2 — lists every model portfolio and its funds. Available to any signed-in shopper.
/// </summary>
public class ListModelsEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/models",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IManagedPortfolioService service, CancellationToken ct) =>
            {
                var models = await service.ListModelsAsync(ct);
                return Results.Ok(models.Select(m => new
                {
                    modelId = m.ModelId,
                    name = m.Name,
                    funds = m.Funds.Select(f => new { instrumentId = f.InstrumentId, weight = f.Weight })
                }));
            })
            .Produces<object>()
            .WithTags("PortfolioEndpoints");
    }
}
