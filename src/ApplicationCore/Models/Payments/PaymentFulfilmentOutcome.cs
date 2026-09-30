namespace Microsoft.eShopWeb.ApplicationCore.Models.Payments;

public class PaymentFulfilmentOutcome
{
    public int OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CaptureId { get; set; } = string.Empty;
    public decimal CapturedAmount { get; set; }
    public decimal? PayPalFee { get; set; }
    public decimal? NetAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
}
