using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BlazorShared.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Identity;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

/// <summary>
/// Subscribe the calling user to a plan. Idempotent per user and plan: repeating the call (double-click, client
/// retry) returns the existing subscription instead of creating a second one.
/// </summary>
public class CreateSubscriptionEndpoint : IEndpoint<IResult, CreateSubscriptionRequest, ISubscriptionService, CancellationToken>
{
    private const int MaxPlanHandleLength = 128;
    private const int MaxNameLength = 100;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/subscriptions",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreateSubscriptionRequest request, ClaimsPrincipal user, UserManager<ApplicationUser> userManager,
                ISubscriptionService subscriptionService, CancellationToken cancellationToken) =>
            {
                var userName = user.Identity?.Name;
                if (string.IsNullOrWhiteSpace(userName))
                {
                    return Results.Forbid();
                }

                // Identity comes from the token; the e-mail from the identity store when the account exists there.
                var account = await userManager.FindByNameAsync(userName);
                var email = account?.Email ?? (userName.Contains('@') ? userName : null);
                if (string.IsNullOrWhiteSpace(email))
                {
                    return Error(StatusCodes.Status400BadRequest, "Your account has no e-mail address to bill against.");
                }

                request.Subscriber = BuildSubscriber(userName, email, request.FirstName, request.LastName);
                return await HandleAsync(request, subscriptionService, cancellationToken);
            })
            .Produces<CreateSubscriptionResponse>(StatusCodes.Status201Created)
            .Produces<CreateSubscriptionResponse>(StatusCodes.Status200OK)
            .Produces<ErrorDetails>(StatusCodes.Status400BadRequest)
            .Produces<ErrorDetails>(StatusCodes.Status409Conflict)
            .WithTags("SubscriptionEndpoints");
    }

    public async Task<IResult> HandleAsync(CreateSubscriptionRequest request, ISubscriptionService subscriptionService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PlanHandle) || request.PlanHandle.Length > MaxPlanHandleLength)
        {
            return Error(StatusCodes.Status400BadRequest, "planHandle is required. See GET /api/subscription-plans for the available plans.");
        }

        if (request.Subscriber is null)
        {
            return Results.Forbid();
        }

        var response = new CreateSubscriptionResponse(request.CorrelationId());
        var result = await subscriptionService.SubscribeAsync(request.Subscriber, request.PlanHandle, cancellationToken);

        switch (result.Outcome)
        {
            case SubscribeOutcome.UnknownPlan:
                return Error(StatusCodes.Status400BadRequest,
                    $"'{request.PlanHandle}' is not an available plan. See GET /api/subscription-plans for the available plans.");
            case SubscribeOutcome.InProgress:
                return Error(StatusCodes.Status409Conflict,
                    "A subscription to this plan is already being created for your account. Check GET /api/my-subscriptions shortly.");
            case SubscribeOutcome.AlreadySubscribed:
                response.Created = false;
                response.Subscription = SubscriptionDto.From(result.Subscription!, result.Plan);
                return Results.Ok(response);
            default:
                response.Created = true;
                response.Subscription = SubscriptionDto.From(result.Subscription!, result.Plan);
                return Results.Created("api/my-subscriptions", response);
        }
    }

    private static Subscriber BuildSubscriber(string userName, string email, string? firstName, string? lastName)
    {
        // Maxio requires first and last names; derive them from the e-mail when the caller does not supply them.
        var localPart = email.Split('@')[0];
        var parts = localPart.Split('.', '_', '-', '+').Where(p => p.Length > 0).ToArray();
        var derivedFirst = parts.Length > 0 ? Capitalize(parts[0]) : "eShop";
        var derivedLast = parts.Length > 1 ? Capitalize(parts[^1]) : "Shopper";

        return new Subscriber(userName, email,
            Clean(firstName) ?? derivedFirst,
            Clean(lastName) ?? derivedLast);
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > MaxNameLength ? trimmed[..MaxNameLength] : trimmed;
    }

    private static string Capitalize(string value) =>
        char.ToUpper(value[0], CultureInfo.InvariantCulture) + value[1..];

    // Same body shape as ExceptionMiddleware produces for every other error.
    private static IResult Error(int statusCode, string message) =>
        Results.Content(new ErrorDetails { StatusCode = statusCode, Message = message }.ToString(),
            "application/json", statusCode: statusCode);
}
