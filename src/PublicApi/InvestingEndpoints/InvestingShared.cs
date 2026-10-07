using System.Security.Claims;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>A request scoped to the authenticated shopper, with identity taken from the token.</summary>
public record ShopperScopedRequest(string ShopperId);

/// <summary>Helpers shared by the investing endpoints.</summary>
internal static class InvestingShared
{
    /// <summary>The shopper's identity, taken from the authenticated token.</summary>
    public static string? ShopperId(ClaimsPrincipal user) => user.Identity?.Name;

    public static string ToText(this EnrolmentStatus status) => status switch
    {
        EnrolmentStatus.Active => "active",
        EnrolmentStatus.Rejected => "rejected",
        _ => "pending"
    };

    public static string ToText(this InvestmentStatus status) => status switch
    {
        InvestmentStatus.Settled => "settled",
        InvestmentStatus.Failed => "failed",
        _ => "pending"
    };
}
