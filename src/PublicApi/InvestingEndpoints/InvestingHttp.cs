using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Shared helpers for the investing endpoints: wire status strings and provider-error mapping.</summary>
internal static class InvestingHttp
{
    public const string Tag = "InvestingEndpoints";

    public static string ToWire(InvestorStatus status) => status switch
    {
        InvestorStatus.Active => "active",
        InvestorStatus.Rejected => "rejected",
        _ => "pending"
    };

    public static string ToWire(InvestmentStatus status) => status switch
    {
        InvestmentStatus.Settled => "settled",
        InvestmentStatus.Failed => "failed",
        _ => "pending"
    };

    /// <summary>
    /// Map an Upvest failure to a caller-facing result: a provider rejection of the caller's input is
    /// surfaced as that 4xx; our-credential, rate-limit, transport and unknown failures become 5xx. The
    /// provider's raw body is never leaked.
    /// </summary>
    public static IResult Problem(UpvestIntegrationException ex)
    {
        if (ex.IsClientError && ex.StatusCode is { } clientStatus)
        {
            return Results.Problem(detail: ex.Message, statusCode: (int)clientStatus);
        }

        var status = ex.StatusCode == HttpStatusCode.TooManyRequests ? 503 : 502;
        return Results.Problem(detail: "The investing provider is currently unavailable. Please try again later.", statusCode: status);
    }
}
