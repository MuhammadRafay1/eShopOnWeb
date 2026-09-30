namespace Microsoft.eShopWeb.ApplicationCore.Models.Payments;

public class RefundSummary
{
    public string RefundId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}
