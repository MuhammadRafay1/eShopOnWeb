using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Swashbuckle.AspNetCore.Annotations;
using Roles = BlazorShared.Authorization.Constants.Roles;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Combined route + body for creating/replacing a model portfolio.</summary>
public class UpsertModelApiRequest
{
    [FromRoute(Name = "modelId")] public string ModelId { get; set; } = default!;
    [FromBody] public ModelUpsertApiRequest Body { get; set; } = default!;
}

/// <summary>Flow 2 — operator creates or replaces a model portfolio. Administrator only.</summary>
[Authorize(Roles = Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class UpsertModelEndpoint : EndpointBaseAsync
    .WithRequest<UpsertModelApiRequest>
    .WithActionResult<ModelApiResponse>
{
    private readonly IModelPortfolioService _service;

    public UpsertModelEndpoint(IModelPortfolioService service) => _service = service;

    [HttpPut("api/portfolio/models/{modelId}")]
    [SwaggerOperation(Summary = "Create or replace a model portfolio (operator)", Tags = new[] { "Portfolio" })]
    public override async Task<ActionResult<ModelApiResponse>> HandleAsync(
        UpsertModelApiRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.Body?.Funds is null)
            return BadRequest("name and funds are required.");

        var funds = request.Body.Funds
            .Select(f => new FundWeight(f.InstrumentId, f.Weight))
            .ToList();

        try
        {
            var result = await _service.UpsertModelAsync(request.ModelId, request.Body.Name, funds, cancellationToken);
            return Ok(PortfolioApi.ToResponse(result));
        }
        catch (PortfolioException ex)
        {
            return PortfolioApi.ToErrorResult(ex);
        }
    }
}

/// <summary>Flow 2 — every model portfolio and its funds. Any signed-in shopper.</summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ListModelsEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<List<ModelApiResponse>>
{
    private readonly IModelPortfolioService _service;

    public ListModelsEndpoint(IModelPortfolioService service) => _service = service;

    [HttpGet("api/portfolio/models")]
    [SwaggerOperation(Summary = "List model portfolios", Tags = new[] { "Portfolio" })]
    public override async Task<ActionResult<List<ModelApiResponse>>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var models = await _service.ListModelsAsync(cancellationToken);
        return Ok(models.Select(PortfolioApi.ToResponse).ToList());
    }
}

/// <summary>Flow 2 — the signed-in investor chooses which model their portfolio follows.</summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ChooseModelEndpoint : EndpointBaseAsync
    .WithRequest<ChooseModelApiRequest>
    .WithActionResult
{
    private readonly IModelPortfolioService _service;

    public ChooseModelEndpoint(IModelPortfolioService service) => _service = service;

    [HttpPut("api/portfolio/investor/model")]
    [SwaggerOperation(Summary = "Choose the model the caller's portfolio follows", Tags = new[] { "Portfolio" })]
    public override async Task<ActionResult> HandleAsync(
        [FromBody] ChooseModelApiRequest request, CancellationToken cancellationToken = default)
    {
        var shopperId = PortfolioApi.ShopperId(User);
        if (shopperId is null) return Unauthorized();
        if (request is null || string.IsNullOrWhiteSpace(request.ModelId))
            return BadRequest("modelId is required.");

        try
        {
            await _service.ChooseModelAsync(shopperId, request.ModelId, cancellationToken);
            return NoContent();
        }
        catch (PortfolioException ex)
        {
            return PortfolioApi.ToErrorResult(ex);
        }
    }
}
