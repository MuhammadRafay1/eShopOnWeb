using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Operator action: creates or replaces a model portfolio.</summary>
public class UpsertModelEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/models/{modelId}",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (string modelId, UpsertModelRequest request, IPortfolioService service, CancellationToken ct) =>
                await PortfolioEndpointHelpers.RunAsync(async () =>
                {
                    var funds = PortfolioRequestMapper.ToFunds(request.Funds);
                    var view = await service.UpsertModelAsync(modelId, request.Name ?? string.Empty, funds, ct);
                    return Results.Ok(ToResponse(view));
                }))
            .Produces<ModelResponse>()
            .WithTags("PortfolioEndpoints");
    }

    internal static ModelResponse ToResponse(ModelView view) =>
        new(view.ModelId, view.Name, view.Funds.Select(f => new ModelFundDto { InstrumentId = f.InstrumentId, Weight = f.Weight }).ToList());
}
