using System.Collections.Generic;
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
/// GET /api/portfolio/models — every model portfolio and its funds (any signed-in shopper).
/// </summary>
public class GetModelsEndpoint : IEndpoint<IResult, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/models",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IPortfolioService service, CancellationToken ct) =>
            {
                var models = await service.GetModelsAsync(ct);
                var response = models.Select(m => new ModelResponse
                {
                    ModelId = m.ModelId,
                    Name = m.Name,
                    Funds = m.Funds.Select(f => new FundDto { InstrumentId = f.InstrumentId, Weight = f.Weight }).ToList(),
                }).ToList();
                return Results.Ok(response);
            })
            .Produces<List<ModelResponse>>()
            .WithTags("PortfolioEndpoints");
    }

    // Logic is in the route lambda (it needs the cancellation token).
    public Task<IResult> HandleAsync(IPortfolioService service) => Task.FromResult(Results.BadRequest() as IResult);
}
