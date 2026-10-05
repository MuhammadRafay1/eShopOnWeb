using System.Security.Claims;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

internal static class InvestingShared
{
    /// <summary>The caller's stable identity, used as the shopper's BuyerId. Taken from the token.</summary>
    public static string? GetBuyerId(this ClaimsPrincipal? user)
    {
        if (user is null) return null;
        var name = user.Identity?.Name;
        if (!string.IsNullOrEmpty(name)) return name;
        return user.FindFirstValue(ClaimTypes.Name)
            ?? user.FindFirstValue("unique_name")
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    public static decimal ToEuros(long cents) => cents / 100m;

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
