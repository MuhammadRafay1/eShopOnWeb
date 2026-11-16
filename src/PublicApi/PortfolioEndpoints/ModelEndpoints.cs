using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.PublicApi.Portfolio;
using Microsoft.eShopWeb.PublicApi.Portfolio.Services;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Flow 2 — create or replace a model portfolio (operator only).</summary>
public class UpsertModelEndpoint : IEndpoint<IResult, UpsertModelRequest, IModelPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/models/{modelId}",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string modelId, UpsertModelRequest request, IModelPortfolioService service, HttpContext http, CancellationToken ct) =>
            {
                request.ModelId = modelId;
                request.CallerId = http.User.ShopperId();
                request.Cancellation = ct;
                return await HandleAsync(request, service);
            })
            .Produces<ModelResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public Task<IResult> HandleAsync(UpsertModelRequest request, IModelPortfolioService service) =>
        EndpointSupport.Guarded(async () =>
        {
            var funds = (request.Funds ?? new List<FundDto>())
                .Select(f => new UpvestFund(f.InstrumentId, f.Weight))
                .ToList();
            var result = await service.UpsertAsync(request.ModelId, request.Name, funds, request.Cancellation);
            return Results.Ok(ToResponse(result));
        });

    internal static ModelResponse ToResponse(ModelResult result) => new()
    {
        ModelId = result.ModelId,
        Name = result.Name,
        Funds = result.Funds.Select(f => new FundDto { InstrumentId = f.InstrumentId, Weight = f.Weight }).ToList(),
    };
}

/// <summary>Flow 2 — every model portfolio and its funds.</summary>
public class ListModelsEndpoint : IEndpoint<IResult, ListModelsRequest, IModelPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/models",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IModelPortfolioService service, CancellationToken ct) =>
            {
                return await HandleAsync(new ListModelsRequest { Cancellation = ct }, service);
            })
            .Produces<List<ModelResponse>>()
            .WithTags("PortfolioEndpoints");
    }

    public Task<IResult> HandleAsync(ListModelsRequest request, IModelPortfolioService service) =>
        EndpointSupport.Guarded(async () =>
        {
            var models = await service.ListAsync(request.Cancellation);
            return Results.Ok(models.Select(UpsertModelEndpoint.ToResponse).ToList());
        });
}

/// <summary>Flow 2 — the signed-in investor chooses which model their portfolio follows.</summary>
public class SelectModelEndpoint : IEndpoint<IResult, SelectModelRequest, IModelPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/investor/model",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (SelectModelRequest request, IModelPortfolioService service, HttpContext http, CancellationToken ct) =>
            {
                request.CallerId = http.User.ShopperId();
                request.Cancellation = ct;
                return await HandleAsync(request, service);
            })
            .Produces(StatusCodes.Status204NoContent)
            .WithTags("PortfolioEndpoints");
    }

    public Task<IResult> HandleAsync(SelectModelRequest request, IModelPortfolioService service) =>
        EndpointSupport.Guarded(async () =>
        {
            await service.SelectAsync(request.CallerId, request.ModelId, request.Cancellation);
            return Results.NoContent();
        });
}
