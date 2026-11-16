using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Operator action: create or replace a model portfolio. Restricted to administrators.</summary>
[Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class UpsertModelEndpoint : EndpointBaseAsync
    .WithRequest<UpsertModelBody>
    .WithActionResult<ModelResponse>
{
    private readonly IPortfolioService _portfolioService;

    public UpsertModelEndpoint(IPortfolioService portfolioService) => _portfolioService = portfolioService;

    [HttpPut("api/portfolio/models/{modelId}")]
    [SwaggerOperation(Summary = "Create or replace a model portfolio (operator)", Tags = new[] { "PortfolioEndpoints" })]
    public override async Task<ActionResult<ModelResponse>> HandleAsync([FromBody] UpsertModelBody request, CancellationToken cancellationToken = default)
    {
        var modelId = (string)RouteData.Values["modelId"]!;
        var funds = (request.Funds ?? new List<FundDto>())
            .Select(f => new FundWeight(f.InstrumentId, f.Weight))
            .ToList();

        var model = await _portfolioService.UpsertModelAsync(modelId, request.Name, funds, cancellationToken);
        return Ok(ModelEndpointMapping.ToResponse(model));
    }
}

/// <summary>Lists every model portfolio and its funds. Any signed-in shopper.</summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ListModelsEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<IEnumerable<ModelResponse>>
{
    private readonly IPortfolioService _portfolioService;

    public ListModelsEndpoint(IPortfolioService portfolioService) => _portfolioService = portfolioService;

    [HttpGet("api/portfolio/models")]
    [SwaggerOperation(Summary = "List model portfolios", Tags = new[] { "PortfolioEndpoints" })]
    public override async Task<ActionResult<IEnumerable<ModelResponse>>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var models = await _portfolioService.ListModelsAsync(cancellationToken);
        return Ok(models.Select(ModelEndpointMapping.ToResponse).ToList());
    }
}

/// <summary>The signed-in investor chooses which model their portfolio follows.</summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ChooseModelEndpoint : EndpointBaseAsync
    .WithRequest<ChooseModelRequest>
    .WithActionResult<InvestorResponse>
{
    private readonly IPortfolioService _portfolioService;

    public ChooseModelEndpoint(IPortfolioService portfolioService) => _portfolioService = portfolioService;

    [HttpPut("api/portfolio/investor/model")]
    [SwaggerOperation(Summary = "Choose a model portfolio to follow", Tags = new[] { "PortfolioEndpoints" })]
    public override async Task<ActionResult<InvestorResponse>> HandleAsync([FromBody] ChooseModelRequest request, CancellationToken cancellationToken = default)
    {
        var userName = User.Identity!.Name!;
        var investor = await _portfolioService.ChooseModelAsync(userName, request.ModelId, cancellationToken);
        return Ok(RegisterInvestorEndpoint.ToResponse(investor));
    }
}

internal static class ModelEndpointMapping
{
    public static ModelResponse ToResponse(ModelPortfolio model) => new()
    {
        ModelId = model.ModelId,
        Name = model.Name,
        Funds = model.Funds.Select(f => new FundDto { InstrumentId = f.InstrumentId, Weight = f.Weight }).ToList()
    };
}
