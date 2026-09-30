namespace Microsoft.eShopWeb.ApplicationCore.Models.Payments;

public class PaymentCancellationOutcome
{
    public int OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
}
