namespace Microsoft.eShopWeb.ApplicationCore.Models.Payments;

public class PaymentAuthorizationOutcome
{
    public int OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string AuthorizationId { get; set; } = string.Empty;
    public decimal HeldAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
}
