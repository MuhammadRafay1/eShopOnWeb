using System.Collections.Generic;
using System.Linq;
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

/// <summary>The signed-in shopper's investments, newest first, each with its settlement status (Flow 3/4).</summary>
public class InvestmentsListEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/investments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                HttpContext http, IInvestingService investing) =>
            {
                var shopperId = http.User.Identity?.Name;
                if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();

                try
                {
                    var investor = await investing.GetInvestorAsync(shopperId, http.RequestAborted);
                    if (investor is null) return Results.NotFound(new { error = "You are not enrolled in investing." });

                    var investments = investor.Investments
                        .OrderByDescending(i => i.CreatedDate)
                        .ThenByDescending(i => i.Id)
                        .Select(i => new InvestmentResponse
                        {
                            InvestmentId = i.Id,
                            Amount = i.AmountInCents / 100m,
                            Status = InvestingHttp.ToWire(i.Status)
                        })
                        .ToList();

                    return Results.Ok(investments);
                }
                catch (UpvestIntegrationException ex)
                {
                    return InvestingHttp.Problem(ex);
                }
            })
            .Produces<List<InvestmentResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags(InvestingHttp.Tag);
    }
}

public class InvestmentResponse
{
    public int InvestmentId { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Amount { get; set; }

    public string Status { get; set; } = "pending";
}
