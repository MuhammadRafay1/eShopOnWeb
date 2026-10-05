using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Billing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Identity;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

/// <summary>
/// Subscribes the authenticated user to a plan. Idempotent: subscribing twice
/// to the same plan returns the existing subscription instead of creating a
/// duplicate.
/// </summary>
public class CreateSubscriptionEndpoint : IEndpoint<IResult, CreateSubscriptionRequest, ClaimsPrincipal>
{
    private readonly IBillingService _billingService;
    private readonly UserManager<ApplicationUser> _userManager;

    public CreateSubscriptionEndpoint(IBillingService billingService, UserManager<ApplicationUser> userManager)
    {
        _billingService = billingService;
        _userManager = userManager;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/subscriptions",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreateSubscriptionRequest request, ClaimsPrincipal user) =>
            {
                return await HandleAsync(request, user);
            })
           .Produces<CreateSubscriptionResponse>()
           .WithTags("SubscriptionEndpoints");
    }

    public async Task<IResult> HandleAsync(CreateSubscriptionRequest request, ClaimsPrincipal user)
    {
        var userName = user.Identity?.Name ?? string.Empty;
        var appUser = await _userManager.FindByNameAsync(userName)
            ?? throw new UserNotFoundException(userName);

        var enrollment = await _billingService.EnrollAsync(
            new SubscriberInfo(appUser.Id, appUser.Email ?? userName),
            request.PlanHandle ?? string.Empty);

        var response = new CreateSubscriptionResponse(request.CorrelationId())
        {
            Status = enrollment.AlreadySubscribed ? "AlreadySubscribed" : "Subscribed",
            AlreadySubscribed = enrollment.AlreadySubscribed,
            Subscription = SubscriptionDtoMapper.ToDto(enrollment.Subscription)
        };

        return Results.Ok(response);
    }
}