namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

/// <summary>
/// Application-layer view of the payment settings the domain services need. Bound from the same
/// <c>PayPal:</c> configuration section as the infrastructure client, but kept here so ApplicationCore
/// does not depend on Infrastructure. The single configured currency is used for the whole app.
/// </summary>
public class PaymentSettings
{
    public const string ConfigSection = "PayPal";

    public string Currency { get; set; } = "USD";
}
