using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

/// <summary>
/// List the calling user's subscriptions, read from Maxio (the billing system of record)
/// </summary>
public class MySubscriptionListEndpoint : IEndpoint<IResult, ListMySubscriptionsRequest, ISubscriptionService, CancellationToken>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/my-subscriptions",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, ISubscriptionService subscriptionService, CancellationToken cancellationToken) =>
            {
                var buyerId = user.Identity?.Name;
                if (string.IsNullOrWhiteSpace(buyerId))
                {
                    return Results.Forbid();
                }

                return await HandleAsync(new ListMySubscriptionsRequest(buyerId), subscriptionService, cancellationToken);
            })
            .Produces<ListMySubscriptionsResponse>()
            .WithTags("SubscriptionEndpoints");
    }

    public async Task<IResult> HandleAsync(ListMySubscriptionsRequest request, ISubscriptionService subscriptionService,
        CancellationToken cancellationToken)
    {
        var response = new ListMySubscriptionsResponse(request.CorrelationId());

        var subscriptions = await subscriptionService.GetSubscriptionsAsync(request.BuyerId, cancellationToken);

        response.Subscriptions.AddRange(subscriptions.Select(s => SubscriptionDto.From(s)));
        return Results.Ok(response);
    }
}
