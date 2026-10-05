namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Billing;

/// <summary>
/// The eShopOnWeb user on whose behalf a billing operation runs.
/// </summary>
public record Subscriber(string UserId, string UserName, string Email);