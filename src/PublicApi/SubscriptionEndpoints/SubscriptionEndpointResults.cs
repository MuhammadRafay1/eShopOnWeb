using Microsoft.AspNetCore.Http;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

/// <summary>
/// Maps a subscription-billing failure onto a caller-facing problem response:
/// a caller-fixable rejection keeps its 4xx status; everything the caller cannot
/// fix (our credentials, the provider, unknown outcomes) surfaces as 5xx.
/// </summary>
internal static class SubscriptionEndpointResults
{
    public static IResult Problem(SubscriptionBillingException ex)
    {
        var status = ex.CallerFault
            ? ex.HttpStatusCode ?? 400
            : ex.HttpStatusCode is 503 or 504 ? ex.HttpStatusCode.Value : 502;
        return Results.Problem(ex.Message, statusCode: status);
    }
}