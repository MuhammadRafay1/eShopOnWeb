using System;
using Microsoft.AspNetCore.Http;
using Microsoft.eShopWeb.PublicApi.Maxio;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

/// <summary>
/// Maps Maxio results to the API's response DTOs and Maxio failures to HTTP results.
/// </summary>
public static class SubscriptionMapper
{
    public static SubscriptionPlanDto ToPlanDto(MaxioProduct product) => new()
    {
        Handle = product.Handle ?? string.Empty,
        Name = product.Name,
        Description = product.Description,
        Price = product.PriceInCents / 100m,
        Interval = product.Interval,
        IntervalUnit = product.IntervalUnit ?? string.Empty,
        RequiresPaymentMethod = product.RequireCreditCard,
        HasTrial = product.TrialInterval is not null
    };

    public static SubscriptionDto ToSubscriptionDto(MaxioSubscription subscription) => new()
    {
        Id = subscription.Id,
        PlanHandle = subscription.Product?.Handle ?? string.Empty,
        PlanName = subscription.Product?.Name ?? string.Empty,
        Price = subscription.ProductPriceInCents / 100m,
        State = subscription.State,
        CurrentPeriodStartedAt = subscription.CurrentPeriodStartedAt,
        CurrentPeriodEndsAt = subscription.CurrentPeriodEndsAt,
        NextBillingAt = subscription.NextAssessmentAt ?? subscription.CurrentPeriodEndsAt,
        CreatedAt = subscription.CreatedAt,
        CanceledAt = subscription.CanceledAt,
        BalanceInCents = subscription.BalanceInCents,
        PaymentCollectionMethod = subscription.PaymentCollectionMethod
    };

    /// <summary>
    /// Translates known billing failures into HTTP results. Returns null when the
    /// exception is not billing-specific and must bubble up to the middleware.
    /// </summary>
    public static IResult? MapException(Exception exception)
    {
        switch (exception)
        {
            case SubscriptionPlanNotFoundException planNotFound:
                return Results.Problem(
                    title: "Subscription plan not found",
                    detail: planNotFound.Message,
                    statusCode: StatusCodes.Status404NotFound);
            case MaxioConfigurationException notConfigured:
                return Results.Problem(
                    title: "Billing is not configured",
                    detail: notConfigured.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            case MaxioApiException apiError:
                return Results.Problem(
                    title: apiError.StatusCode == 422
                        ? "The billing system rejected the request"
                        : "The billing system returned an error",
                    detail: apiError.Message,
                    statusCode: apiError.StatusCode == 422
                        ? StatusCodes.Status400BadRequest
                        : StatusCodes.Status502BadGateway);
            default:
                return null;
        }
    }
}