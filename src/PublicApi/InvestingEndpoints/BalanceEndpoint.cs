using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>What the signed-in shopper has set aside and invested so far (Flow 5).</summary>
public class BalanceEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/balance",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                HttpContext http, IInvestingService investing) =>
            {
                var shopperId = http.User.Identity?.Name;
                if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();

                try
                {
                    var investor = await investing.GetInvestorAsync(shopperId, http.RequestAborted);
                    if (investor is null) return Results.NotFound(new { error = "You are not enrolled in investing." });

                    return Results.Ok(new BalanceResponse
                    {
                        PendingAmount = investor.PendingAmountInCents / 100m,
                        InvestedAmount = investor.InvestedAmountInCents / 100m
                    });
                }
                catch (UpvestIntegrationException ex)
                {
                    return InvestingHttp.Problem(ex);
                }
            })
            .Produces<BalanceResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags(InvestingHttp.Tag);
    }
}

public class BalanceResponse
{
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal PendingAmount { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal InvestedAmount { get; set; }
}
