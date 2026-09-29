using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

/// <summary>
/// Result of creating a PayPal order (Orders v2). Field names track the spec's snake_case JSON.
/// If the card was supplied at create time PayPal may already have produced the authorization in a
/// single step, in which case <see cref="Authorization"/> is populated and no separate /authorize
/// call is needed.
/// </summary>
public class PayPalOrderResult
{
    public string Id { get; set; } = "";

    /// <summary>order_status: CREATED | SAVED | APPROVED | VOIDED | COMPLETED | PAYER_ACTION_REQUIRED.</summary>
    public string Status { get; set; } = "";

    /// <summary>The invoice_id sent on the order (globally unique), for reconciliation correlation.</summary>
    public string? InvoiceId { get; set; }

    /// <summary>True when status == PAYER_ACTION_REQUIRED (3DS / buyer redirect needed).</summary>
    public bool PayerActionRequired { get; set; }

    /// <summary>The authorization already present on the order, if PayPal produced it during create.</summary>
    public PayPalAuthorizationResult? Authorization { get; set; }
}

/// <summary>An authorization (the hold) under purchase_units[].payments.authorizations[].</summary>
public class PayPalAuthorizationResult
{
    public string Id { get; set; } = "";

    /// <summary>authorization_status: CREATED | CAPTURED | DENIED | PARTIALLY_CAPTURED | VOIDED | PENDING.</summary>
    public string Status { get; set; } = "";
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "";
    public DateTimeOffset? ExpirationTime { get; set; }
}

/// <summary>A capture (money taken) with its seller_receivable_breakdown.</summary>
public class PayPalCaptureResult
{
    public string Id { get; set; } = "";

    /// <summary>capture_status: COMPLETED | DECLINED | PARTIALLY_REFUNDED | PENDING | REFUNDED | FAILED.</summary>
    public string Status { get; set; } = "";

    /// <summary>gross_amount from seller_receivable_breakdown: the captured amount.</summary>
    public decimal GrossAmount { get; set; }

    /// <summary>paypal_fee from seller_receivable_breakdown.</summary>
    public decimal? PayPalFeeAmount { get; set; }

    /// <summary>net_amount from seller_receivable_breakdown: net proceeds to the merchant.</summary>
    public decimal? NetAmount { get; set; }
    public string CurrencyCode { get; set; } = "";
}

/// <summary>A refund against a capture.</summary>
public class PayPalRefundResult
{
    public string Id { get; set; } = "";

    /// <summary>refund_status: CANCELLED | FAILED | PENDING | COMPLETED.</summary>
    public string Status { get; set; } = "";
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "";
}

/// <summary>A vaulted (saved) card token.</summary>
public class PayPalPaymentTokenResult
{
    /// <summary>The vault token id — the durable "saved card" reference.</summary>
    public string Id { get; set; } = "";

    /// <summary>PayPal-generated customer id grouping this shopper's tokens (if returned).</summary>
    public string? CustomerId { get; set; }

    /// <summary>card_response_entity.brand (VISA, MASTERCARD, ...).</summary>
    public string? Brand { get; set; }

    /// <summary>card_response_entity.last_digits.</summary>
    public string? LastDigits { get; set; }

    /// <summary>card_response_entity.expiry (YYYY-MM).</summary>
    public string? Expiry { get; set; }
}

/// <summary>
/// One PayPal transaction from the Transaction Search report (transaction_info), flattened to the
/// fields reconciliation needs.
/// </summary>
public class PayPalTransactionRecord
{
    public string? TransactionId { get; set; }

    /// <summary>invoice_id echoed from order/capture time (e.g. "eshop-order-42-ab12cd34ef56").</summary>
    public string? InvoiceId { get; set; }

    /// <summary>custom_field echoed from custom_id set at order time (the eShop order id).</summary>
    public string? CustomField { get; set; }

    /// <summary>paypal_reference_id (e.g. the PayPal order id when type == ODR).</summary>
    public string? PayPalReferenceId { get; set; }

    /// <summary>paypal_reference_id_type: ODR | TXN | SUB | PAP.</summary>
    public string? PayPalReferenceIdType { get; set; }

    /// <summary>transaction_event_code (e.g. T0006 auth, T1502 capture, T1107 refund).</summary>
    public string? EventCode { get; set; }

    /// <summary>Single-character transaction_status code (D/P/S/V).</summary>
    public string? Status { get; set; }
    public decimal? Amount { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal? FeeAmount { get; set; }
    public DateTimeOffset? InitiationDate { get; set; }
    public DateTimeOffset? UpdatedDate { get; set; }
}
