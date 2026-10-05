using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Billing;
using Microsoft.eShopWeb.Infrastructure.Identity;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

/// <summary>
/// Resolves the JWT caller (whose token carries the username in the name claim) onto the
/// application user and the <see cref="Subscriber"/> the billing service works with.
/// </summary>
internal static class SubscriptionUserResolution
{
    public static async Task<Subscriber?> ResolveAsync(ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager)
    {
        var username = principal.Identity?.Name;
        if (string.IsNullOrEmpty(username))
        {
            return null;
        }
        var user = await userManager.FindByNameAsync(username);
        if (user is null)
        {
            return null;
        }
        return new Subscriber(user.Id, user.UserName ?? username, user.Email ?? string.Empty);
    }
}