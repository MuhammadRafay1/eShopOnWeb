using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>The caller's investments, newest first.</summary>
public class GetInvestmentsEndpoint : IEndpoint<IResult, EmptyInvestingRequest, IInvestingService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetInvestmentsEndpoint(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/investments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IInvestingService investingService) =>
            {
                return await HandleAsync(new EmptyInvestingRequest(), investingService);
            })
            .Produces<GetInvestmentsResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(EmptyInvestingRequest request, IInvestingService investingService)
    {
        var buyerId = _httpContextAccessor.HttpContext?.User.GetBuyerId();
        if (string.IsNullOrEmpty(buyerId))
            return Results.Unauthorized();

        var investments = await investingService.GetInvestmentsAsync(buyerId);

        var response = new GetInvestmentsResponse(request.CorrelationId())
        {
            Investments = investments.Select(i => new InvestmentDto
            {
                InvestmentId = i.InvestmentId,
                Amount = InvestingShared.ToEuros(i.AmountCents),
                Status = i.Status.ToText()
            }).ToList()
        };
        return Results.Ok(response);
    }
}

public class GetInvestmentsResponse : BaseResponse
{
    public GetInvestmentsResponse(Guid correlationId) : base(correlationId) { }
    public GetInvestmentsResponse() { }

    public List<InvestmentDto> Investments { get; set; } = new();
}

public class InvestmentDto
{
    public Guid InvestmentId { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Amount { get; set; }

    /// <summary>"pending", "settled" or "failed".</summary>
    public string Status { get; set; } = string.Empty;
}
