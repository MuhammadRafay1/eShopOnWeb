using System;
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

/// <summary>Reports what the signed-in shopper has set aside and invested so far (Flow 5).</summary>
public class GetBalanceEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/balance",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                ClaimsPrincipal user,
                IRepository<InvestingAccount> repository) =>
                await HandleAsync(user, repository))
            .Produces<BalanceResponse>()
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

        return Results.Ok(new BalanceResponse
        {
            PendingAmount = account.PendingAmount,
            InvestedAmount = account.InvestedAmount
        });
    }
}

public class BalanceResponse : BaseResponse
{
    public BalanceResponse() { }

    public BalanceResponse(Guid correlationId) : base(correlationId) { }

    /// <summary>Change set aside but not yet invested, in euros.</summary>
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal PendingAmount { get; set; }

    /// <summary>Total invested on the shopper's behalf so far, in euros.</summary>
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal InvestedAmount { get; set; }
}
