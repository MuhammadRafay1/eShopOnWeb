namespace Microsoft.eShopWeb.ApplicationCore.Models.Payments;

/// <summary>One PayPal transaction-search result row, used for reconciliation.</summary>
public class GatewayTransaction
{
    public string TransactionId { get; set; } = string.Empty;
    public string? Status { get; set; }
    public decimal? Amount { get; set; }
    public string? CurrencyCode { get; set; }
    public string? InvoiceId { get; set; }
    public string? CustomField { get; set; }
}
