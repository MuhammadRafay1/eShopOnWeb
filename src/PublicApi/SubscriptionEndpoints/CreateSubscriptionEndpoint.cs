using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Identity;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

/// <summary>
/// Subscribes the authenticated user to a subscription plan
/// </summary>
public class CreateSubscriptionEndpoint : IEndpoint<IResult, CreateSubscriptionRequest, ClaimsPrincipal>
{
    private readonly IMapper _mapper;
    private readonly ISubscriptionBillingService _billingService;
    private readonly UserManager<ApplicationUser> _userManager;

    public CreateSubscriptionEndpoint(IMapper mapper,
        ISubscriptionBillingService billingService,
        UserManager<ApplicationUser> userManager)
    {
        _mapper = mapper;
        _billingService = billingService;
        _userManager = userManager;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/subscriptions",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreateSubscriptionRequest request,
             ClaimsPrincipal user) =>
            {
                return await HandleAsync(request, user);
            })
           .Produces<CreateSubscriptionResponse>()
           .WithTags("SubscriptionEndpoints");
    }

    public async Task<IResult> HandleAsync(CreateSubscriptionRequest request, ClaimsPrincipal user)
    {
        var response = new CreateSubscriptionResponse(request.CorrelationId());

        if (string.IsNullOrWhiteSpace(request.ProductHandle))
        {
            return Results.BadRequest(new { error = "A productHandle is required. Get available handles from GET /api/subscription-plans." });
        }

        var applicationUser = await _userManager.FindByNameAsync(user.Identity?.Name ?? string.Empty);
        if (applicationUser is null)
        {
            return Results.Unauthorized();
        }

        var subscriber = new Subscriber
        {
            UserId = applicationUser.Id,
            Email = applicationUser.Email ?? string.Empty,
            FirstName = DeriveFirstName(applicationUser.Email),
            LastName = "eShop Customer"
        };

        var result = await _billingService.SubscribeAsync(subscriber, request.ProductHandle.Trim());

        response.Subscription = _mapper.Map<SubscriptionDto>(result.Subscription);
        response.AlreadySubscribed = result.AlreadySubscribed;
        return Results.Ok(response);
    }

    private static string DeriveFirstName(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "eShop";
        }

        var localPart = email.Split('@')[0];
        return string.IsNullOrWhiteSpace(localPart) ? "eShop" : localPart;
    }
}