using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.PublicApi.Maxio;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

/// <summary>
/// Lists the available subscription plans (the Maxio products of the configured product family).
/// </summary>
public class SubscriptionPlanListEndpoint : IEndpoint<IResult, IMaxioClient>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/subscription-plans",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IMaxioClient maxioClient) =>
            {
                return await HandleAsync(maxioClient);
            })
           .Produces<ListSubscriptionPlansResponse>()
           .WithTags("SubscriptionEndpoints");
    }

    public async Task<IResult> HandleAsync(IMaxioClient maxioClient)
    {
        try
        {
            var products = await maxioClient.ListFamilyProductsAsync();
            var response = new ListSubscriptionPlansResponse
            {
                Plans = products
                    .Where(p => p.ArchivedAt is null)
                    .OrderBy(p => p.PriceInCents)
                    .Select(SubscriptionMapper.ToPlanDto)
                    .ToList()
            };
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            var mapped = SubscriptionMapper.MapException(ex);
            if (mapped is not null)
            {
                return mapped;
            }

            throw;
        }
    }
}