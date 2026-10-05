using System;
using System.Collections.Generic;
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
/// Subscribes the authenticated user to a plan. Idempotent: repeated calls never create
/// a second Maxio customer or a second live subscription for the same plan.
/// </summary>
/// <remarks>
/// Scoped services (UserManager, ISubscriptionService) are resolved per request via the
/// route handler's parameters - they must not be constructor-injected, because endpoint
/// instances are resolved once at application startup.
/// </remarks>
public class CreateSubscriptionEndpoint : IEndpoint<IResult, CreateSubscriptionRequest, UserManager<ApplicationUser>, ISubscriptionService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CreateSubscriptionEndpoint(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/subscriptions",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreateSubscriptionRequest request, UserManager<ApplicationUser> userManager, ISubscriptionService subscriptionService) =>
            {
                return await HandleAsync(request, userManager, subscriptionService);
            })
           .Produces<CreateSubscriptionResponse>()
           .ProducesValidationProblem()
           .WithTags("SubscriptionEndpoints");
    }

    public async Task<IResult> HandleAsync(CreateSubscriptionRequest request,
        UserManager<ApplicationUser> userManager, ISubscriptionService subscriptionService)
    {
        var response = new CreateSubscriptionResponse(request.CorrelationId());

        if (string.IsNullOrWhiteSpace(request.PlanHandle))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { nameof(request.PlanHandle), new[] { "A plan handle is required, e.g. 'eshop-pro'." } }
            });
        }

        var user = _httpContextAccessor.HttpContext?.User;
        var appUser = user is null ? null : await GetCurrentUserAsync(userManager, user);
        if (appUser is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            var subscription = await subscriptionService.SubscribeAsync(appUser, request.PlanHandle.Trim());
            response.Subscription = SubscriptionMapper.ToSubscriptionDto(subscription);
            return Results.Created($"api/subscriptions/{subscription.Id}", response);
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