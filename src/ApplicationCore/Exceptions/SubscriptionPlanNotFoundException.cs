using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when a subscription plan handle does not exist in the billing system catalog.
/// </summary>
public class SubscriptionPlanNotFoundException : Exception
{
    public SubscriptionPlanNotFoundException(string productHandle)
        : base($"A subscription plan with handle '{productHandle}' was not found.")
    {
        ProductHandle = productHandle;
    }

    public string ProductHandle { get; }
}