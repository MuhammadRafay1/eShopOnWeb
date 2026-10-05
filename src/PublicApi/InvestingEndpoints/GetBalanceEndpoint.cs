using System;
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

/// <summary>What the caller currently has set aside and the total invested so far.</summary>
public class GetBalanceEndpoint : IEndpoint<IResult, EmptyInvestingRequest, IInvestingService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetBalanceEndpoint(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/balance",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IInvestingService investingService) =>
            {
                return await HandleAsync(new EmptyInvestingRequest(), investingService);
            })
            .Produces<GetBalanceResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(EmptyInvestingRequest request, IInvestingService investingService)
    {
        var buyerId = _httpContextAccessor.HttpContext?.User.GetBuyerId();
        if (string.IsNullOrEmpty(buyerId))
            return Results.Unauthorized();

        var balance = await investingService.GetBalanceAsync(buyerId);
        if (balance is null)
            return Results.NotFound();

        return Results.Ok(new GetBalanceResponse(request.CorrelationId())
        {
            PendingAmount = InvestingShared.ToEuros(balance.PendingCents),
            InvestedAmount = InvestingShared.ToEuros(balance.InvestedCents)
        });
    }
}

public class GetBalanceResponse : BaseResponse
{
    public GetBalanceResponse(Guid correlationId) : base(correlationId) { }
    public GetBalanceResponse() { }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal PendingAmount { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal InvestedAmount { get; set; }
}
