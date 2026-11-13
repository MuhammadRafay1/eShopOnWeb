using System.Collections.Generic;
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
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// PUT /api/portfolio/models/{modelId} — operator action: create or replace a model portfolio.
/// </summary>
public class PutModelEndpoint : IEndpoint<IResult, PutModelRequest, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/models/{modelId}",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string modelId, PutModelRequest request, IPortfolioService service, CancellationToken ct) =>
            {
                request.Bind(modelId, ct);
                return await HandleAsync(request, service);
            })
            .Produces<ModelResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(PutModelRequest request, IPortfolioService service)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new PortfolioValidationException("name is required.");
        }
        if (request.Funds is null || request.Funds.Count == 0)
        {
            throw new PortfolioValidationException("funds is required and must contain at least one fund.");
        }

        var funds = request.Funds
            .Select(f => new FundWeight(
                string.IsNullOrWhiteSpace(f.InstrumentId)
                    ? throw new PortfolioValidationException("Each fund requires an instrumentId (ISIN).")
                    : f.InstrumentId.Trim().ToUpperInvariant(),
                f.Weight))
            .ToList();

        var model = await service.UpsertModelAsync(request.ModelId, request.Name.Trim(), funds, request.Cancellation);

        return Results.Ok(new ModelResponse
        {
            ModelId = model.ModelId,
            Name = model.Name,
            Funds = model.Funds.Select(f => new FundDto { InstrumentId = f.InstrumentId, Weight = f.Weight }).ToList(),
        });
    }
}
