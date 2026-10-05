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

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>The caller's investments, newest first (Flow 3).</summary>
public class InvestmentsEndpoint : IEndpoint<IResult, IInvestingService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public InvestmentsEndpoint(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/investments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (IInvestingService investingService) => await HandleAsync(investingService))
            .Produces<List<InvestmentDto>>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(IInvestingService investingService)
    {
        var buyerId = CallerId.Resolve(_httpContextAccessor.HttpContext?.User);
        if (string.IsNullOrEmpty(buyerId))
            return Results.Unauthorized();

        var investments = await investingService.GetInvestmentsAsync(buyerId, _httpContextAccessor.HttpContext!.RequestAborted);
        var dtos = investments
            .Select(i => new InvestmentDto { InvestmentId = i.InvestmentId, Amount = MoneyFormat.Euros(i.AmountCents), Status = i.Status })
            .ToList();
        return Results.Ok(dtos);
    }
}

public class InvestmentDto
{
    public int InvestmentId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "pending";
}
