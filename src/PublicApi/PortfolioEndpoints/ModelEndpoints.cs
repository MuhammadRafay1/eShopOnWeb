using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;
using static Microsoft.eShopWeb.PublicApi.PortfolioEndpoints.PortfolioEndpointHelpers;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Flow 2 — operator action: create or replace a model portfolio.</summary>
public class UpsertModelEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/models/{modelId}",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (string modelId, UpsertModelRequest request, IPortfolioService service, CancellationToken ct) =>
            {
                var funds = request.Funds.Select(f => new FundView(f.InstrumentId, f.Weight));
                var result = await service.UpsertModelAsync(modelId, request.Name, funds, ct);
                return ToHttpResult(result, ToModelResponse);
            })
            .Produces<ModelResponse>()
            .WithTags("PortfolioEndpoints");
    }

    internal static ModelResponse ToModelResponse(ModelView v) =>
        new(v.ModelId, v.Name, v.Funds.Select(f => new FundResponse(f.InstrumentId, f.Weight)).ToList());
}

/// <summary>Flow 2 — list every model portfolio and its funds.</summary>
public class ListModelsEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/models",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (IPortfolioService service, CancellationToken ct) =>
            {
                var result = await service.GetModelsAsync(ct);
                return ToHttpResult(result, models => (object)models.Select(UpsertModelEndpoint.ToModelResponse).ToList());
            })
            .Produces<System.Collections.Generic.List<ModelResponse>>()
            .WithTags("PortfolioEndpoints");
    }
}

/// <summary>Flow 2 — the signed-in investor chooses which model their portfolio follows.</summary>
public class SelectModelEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/investor/model",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (SelectModelRequest request, ClaimsPrincipal user, IPortfolioService service, CancellationToken ct) =>
            {
                var buyerId = GetBuyerId(user);
                if (string.IsNullOrEmpty(buyerId))
                {
                    return Unauthenticated();
                }

                var result = await service.SelectModelAsync(buyerId, request.ModelId, ct);
                return ToHttpResult(result);
            })
            .WithTags("PortfolioEndpoints");
    }
}
