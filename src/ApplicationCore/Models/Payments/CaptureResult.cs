namespace Microsoft.eShopWeb.ApplicationCore.Models.Payments;

public class CaptureResult
{
    public string CaptureId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal CapturedAmount { get; set; }
    public decimal? PayPalFee { get; set; }
    public decimal? NetAmount { get; set; }
}
