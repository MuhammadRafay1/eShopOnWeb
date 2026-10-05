using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionPlanEndpoints;

/// <summary>
/// Lists the available subscription plans (the subscribable catalog)
/// </summary>
public class SubscriptionPlanListEndpoint : IEndpoint<IResult, IBillingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/subscription-plans",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IBillingService billingService) =>
            {
                return await HandleAsync(billingService);
            })
           .Produces<ListSubscriptionPlansResponse>()
           .WithTags("SubscriptionPlanEndpoints");
    }

    public async Task<IResult> HandleAsync(IBillingService billingService)
    {
        var plans = await billingService.ListPlansAsync();

        var response = new ListSubscriptionPlansResponse();
        response.Plans.AddRange(plans.Select(p => new SubscriptionPlanDto
        {
            Handle = p.Handle,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            Currency = p.Currency,
            IntervalCount = p.IntervalCount,
            IntervalUnit = p.IntervalUnit
        }));

        return Results.Ok(response);
    }
}