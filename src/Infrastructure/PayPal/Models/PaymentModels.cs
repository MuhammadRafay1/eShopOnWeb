using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.Infrastructure.PayPal.Models;

public class AuthorizationResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public Money? Amount { get; set; }

    [JsonPropertyName("expiration_time")]
    public System.DateTimeOffset? ExpirationTime { get; set; }
}

public class ReauthorizeRequest
{
    [JsonPropertyName("amount")]
    public Money Amount { get; set; } = new();
}

public class CaptureRequest
{
    [JsonPropertyName("amount")]
    public Money? Amount { get; set; }

    [JsonPropertyName("final_capture")]
    public bool FinalCapture { get; set; } = true;

    [JsonPropertyName("invoice_id")]
    public string? InvoiceId { get; set; }
}

public class CaptureResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("seller_receivable_breakdown")]
    public SellerReceivableBreakdown? SellerReceivableBreakdown { get; set; }
}

public class RefundRequest
{
    [JsonPropertyName("amount")]
    public Money? Amount { get; set; }

    [JsonPropertyName("invoice_id")]
    public string? InvoiceId { get; set; }
}

public class SellerPayableBreakdown
{
    [JsonPropertyName("total_refunded_amount")]
    public Money? TotalRefundedAmount { get; set; }

    [JsonPropertyName("gross_amount")]
    public Money? GrossAmount { get; set; }
}

public class RefundResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("seller_payable_breakdown")]
    public SellerPayableBreakdown? SellerPayableBreakdown { get; set; }
}
