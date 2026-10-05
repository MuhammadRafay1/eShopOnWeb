using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.PublicApi.Investing;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Lists the signed-in shopper's investments, newest first (Flow 3).</summary>
public class GetInvestmentsEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/investments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                ClaimsPrincipal user,
                IRepository<InvestingAccount> repository) =>
                await HandleAsync(user, repository))
            .Produces<InvestmentsResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("InvestingEndpoints");
    }

    private static async Task<IResult> HandleAsync(ClaimsPrincipal user, IRepository<InvestingAccount> repository)
    {
        var buyerId = user.Identity?.Name;
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));

        var account = await repository.FirstOrDefaultAsync(new InvestingAccountByBuyerSpec(buyerId!));
        if (account is null)
        {
            return Results.NotFound();
        }

        var response = new InvestmentsResponse();
        response.Investments.AddRange(account.Investments
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvestmentDto
            {
                InvestmentId = i.PublicId,
                Amount = i.Amount,
                Status = i.Status.ToText()
            }));
        return Results.Ok(response);
    }
}

public class InvestmentsResponse : BaseResponse
{
    public List<InvestmentDto> Investments { get; set; } = new();
}

public class InvestmentDto
{
    public Guid InvestmentId { get; set; }

    /// <summary>The amount invested, in euros.</summary>
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Amount { get; set; }

    /// <summary>"pending" until the outcome at Upvest is known, then "settled" or "failed".</summary>
    public string Status { get; set; } = string.Empty;
}
