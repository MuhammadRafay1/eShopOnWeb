using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.Infrastructure.Identity;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

/// <summary>
/// Lists the subscriptions that belong to the authenticated user.
/// </summary>
/// <remarks>
/// Scoped services (UserManager, ISubscriptionService) are resolved per request via the
/// route handler's parameters - they must not be constructor-injected, because endpoint
/// instances are resolved once at application startup.
/// </remarks>
public class MySubscriptionListEndpoint : IEndpoint<IResult, UserManager<ApplicationUser>, ISubscriptionService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MySubscriptionListEndpoint(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/my-subscriptions",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (UserManager<ApplicationUser> userManager, ISubscriptionService subscriptionService) =>
            {
                return await HandleAsync(userManager, subscriptionService);
            })
           .Produces<ListMySubscriptionsResponse>()
           .WithTags("SubscriptionEndpoints");
    }

    public async Task<IResult> HandleAsync(UserManager<ApplicationUser> userManager,
        ISubscriptionService subscriptionService)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var appUser = user is null ? null : await GetCurrentUserAsync(userManager, user);
        if (appUser is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            var subscriptions = await subscriptionService.GetSubscriptionsAsync(appUser);
            var response = new ListMySubscriptionsResponse
            {
                Subscriptions = subscriptions
                    .Select(SubscriptionMapper.ToSubscriptionDto)
                    .OrderByDescending(s => s.CreatedAt)
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

    /// <summary>
    /// Resolves the eShopOnWeb user from the JWT's name claim (the token issued by
    /// this API carries the username, not a user id claim).
    /// </summary>
    private static async Task<ApplicationUser?> GetCurrentUserAsync(UserManager<ApplicationUser> userManager, ClaimsPrincipal principal)
    {
        var userName = principal.Identity?.Name ?? principal.FindFirstValue(ClaimTypes.Name);
        return userName is null ? null : await userManager.FindByNameAsync(userName);
    }
}