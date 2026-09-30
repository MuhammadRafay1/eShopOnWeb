namespace Microsoft.eShopWeb.ApplicationCore.Models.Payments;

public class PaymentRefundOutcome
{
    public string RefundId { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalRefunded { get; set; }
    public decimal CapturedAmount { get; set; }
}
