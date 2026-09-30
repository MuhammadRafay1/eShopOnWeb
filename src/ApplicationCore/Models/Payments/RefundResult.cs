namespace Microsoft.eShopWeb.ApplicationCore.Models.Payments;

public class RefundResult
{
    public string RefundId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
