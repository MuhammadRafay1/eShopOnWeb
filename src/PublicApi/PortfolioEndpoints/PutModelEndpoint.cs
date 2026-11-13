using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Flow 2 — operator action: create or replace a model portfolio.
/// Restricted to the administrator role.
/// </summary>
public class PutModelEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/models/{modelId:int}",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int modelId, PutModelRequest request, IManagedPortfolioService service, CancellationToken ct) =>
            {
                if (request is null || string.IsNullOrWhiteSpace(request.Name))
                {
                    return Results.BadRequest(new { message = "name is required." });
                }
                if (request.Funds is null || request.Funds.Count == 0)
                {
                    return Results.BadRequest(new { message = "At least one fund is required." });
                }

                var definition = new ModelDefinition(
                    request.Name,
                    request.Funds.Select(f => new FundInput(f.InstrumentId, f.Weight)).ToList());

                try
                {
                    var model = await service.PutModelAsync(modelId, definition, ct);
                    return Results.Ok(new
                    {
                        modelId = model.ModelId,
                        name = model.Name,
                        funds = model.Funds.Select(f => new { instrumentId = f.InstrumentId, weight = f.Weight })
                    });
                }
                catch (InvalidModelPortfolioException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            })
            .Produces<object>()
            .WithTags("PortfolioEndpoints");
    }
}
