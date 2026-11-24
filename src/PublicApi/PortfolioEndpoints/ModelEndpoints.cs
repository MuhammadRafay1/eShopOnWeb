using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Operator action: create or replace a model portfolio.</summary>
public class UpsertModelEndpoint : IEndpoint<IResult, UpsertModelCommand, IManagedPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/models/{modelId}",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string modelId, UpsertModelRequest request, IManagedPortfolioService service) =>
                await HandleAsync(new UpsertModelCommand(modelId, request?.Name ?? string.Empty, request?.Funds ?? new List<FundRequest>()), service))
            .Produces<ModelResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(UpsertModelCommand command, IManagedPortfolioService service)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return Results.BadRequest("name is required.");
        }
        if (command.Funds is null || command.Funds.Count == 0)
        {
            return Results.BadRequest("funds must contain at least one entry.");
        }

        var funds = command.Funds.Select(f => new ModelFundInput(f.InstrumentId, f.Weight)).ToList();
        var model = await service.UpsertModelAsync(command.ModelId, command.Name, funds);
        return Results.Ok(model.ToResponse());
    }
}

/// <summary>Lists every model portfolio and its funds.</summary>
public class ListModelsEndpoint : IEndpoint<IResult, IManagedPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/models",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IManagedPortfolioService service) => await HandleAsync(service))
            .Produces<IReadOnlyList<ModelResponse>>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(IManagedPortfolioService service)
    {
        var models = await service.ListModelsAsync();
        return Results.Ok(models.Select(m => m.ToResponse()).ToList());
    }
}

/// <summary>The signed-in investor chooses which model their portfolio follows.</summary>
public class ChooseModelEndpoint : IEndpoint<IResult, ChooseModelRequest, IManagedPortfolioService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ChooseModelEndpoint(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/investor/model",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ChooseModelRequest request, IManagedPortfolioService service) => await HandleAsync(request, service))
            .Produces<InvestorResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(ChooseModelRequest request, IManagedPortfolioService service)
    {
        var shopperId = PortfolioMapping.ShopperId(_httpContextAccessor.HttpContext?.User);
        if (string.IsNullOrEmpty(shopperId))
        {
            return Results.Unauthorized();
        }
        if (request is null || string.IsNullOrWhiteSpace(request.ModelId))
        {
            return Results.BadRequest("modelId is required.");
        }

        var investor = await service.ChooseModelAsync(shopperId, request.ModelId);
        return Results.Ok(investor.ToResponse());
    }
}
