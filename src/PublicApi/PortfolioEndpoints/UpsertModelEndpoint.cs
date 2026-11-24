using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

public record UpsertModelCommand(string ModelId, UpsertModelRequest Body);

/// <summary>
/// Flow 2 — operator action: create or replace a model portfolio. Restricted to the administrator role.
/// </summary>
public class UpsertModelEndpoint : IEndpoint<IResult, UpsertModelCommand, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/models/{modelId}",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string modelId, UpsertModelRequest request, IPortfolioService service) =>
            {
                return await HandleAsync(new UpsertModelCommand(modelId, request), service);
            })
            .Produces<ModelResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(UpsertModelCommand command, IPortfolioService service)
    {
        if (command.Body is null || string.IsNullOrWhiteSpace(command.Body.Name))
            throw new PortfolioValidationException("'name' is required.");
        if (command.Body.Funds is null || command.Body.Funds.Count == 0)
            throw new PortfolioValidationException("At least one fund is required.");
        if (command.Body.Funds.Any(f => string.IsNullOrWhiteSpace(f.InstrumentId)))
            throw new PortfolioValidationException("Every fund must have an 'instrumentId' (ISIN).");

        var funds = command.Body.Funds
            .Select(f => new ModelFundInput(f.InstrumentId, f.Weight))
            .ToList();

        var view = await service.UpsertModelAsync(command.ModelId, command.Body.Name, funds);
        return Results.Ok(view.ToResponse());
    }
}
