namespace Microsoft.eShopWeb.ApplicationCore.Models.Payments;

/// <summary>
/// A raw, one-off card as supplied by the caller for a single authorize or vault call. Flows only
/// through method parameters into <see cref="Interfaces.IPaymentGateway"/> — never persisted, never
/// logged.
/// </summary>
public class CardDetails
{
    public string Number { get; set; } = string.Empty;

    /// <summary>Format <c>YYYY-MM</c>.</summary>
    public string Expiry { get; set; } = string.Empty;

    public string SecurityCode { get; set; } = string.Empty;
    public string? CardholderName { get; set; }
    public BillingAddressInput? BillingAddress { get; set; }
}
