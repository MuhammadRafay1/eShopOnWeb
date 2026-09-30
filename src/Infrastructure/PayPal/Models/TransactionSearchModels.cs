using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.Infrastructure.PayPal.Models;

public class TransactionInfo
{
    [JsonPropertyName("transaction_id")]
    public string? TransactionId { get; set; }

    [JsonPropertyName("transaction_status")]
    public string? TransactionStatus { get; set; }

    [JsonPropertyName("transaction_amount")]
    public Money? TransactionAmount { get; set; }

    [JsonPropertyName("fee_amount")]
    public Money? FeeAmount { get; set; }

    [JsonPropertyName("transaction_initiation_date")]
    public System.DateTimeOffset? TransactionInitiationDate { get; set; }

    [JsonPropertyName("invoice_id")]
    public string? InvoiceId { get; set; }

    [JsonPropertyName("custom_field")]
    public string? CustomField { get; set; }
}

public class TransactionDetail
{
    [JsonPropertyName("transaction_info")]
    public TransactionInfo? TransactionInfo { get; set; }
}

public class SearchResponse
{
    [JsonPropertyName("transaction_details")]
    public List<TransactionDetail>? TransactionDetails { get; set; }

    [JsonPropertyName("total_items")]
    public int TotalItems { get; set; }

    [JsonPropertyName("total_pages")]
    public int TotalPages { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }
}
