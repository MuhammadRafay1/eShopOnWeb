namespace Microsoft.eShopWeb.ApplicationCore.Models.Payments;

/// <summary>
/// Everything <see cref="Interfaces.IPaymentGateway.AuthorizeAsync"/> needs to place a hold: exactly
/// one of <see cref="Card"/> (one-off) or <see cref="VaultId"/> (a saved card) must be set.
/// </summary>
public class AuthorizeCardPaymentRequest
{
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>Set as the PayPal order's invoice_id — the primary reconciliation key.</summary>
    public string CorrelationReference { get; set; } = string.Empty;

    /// <summary>Set as the PayPal order's custom_id — a best-effort secondary reconciliation key.</summary>
    public string CustomId { get; set; } = string.Empty;

    public CardDetails? Card { get; set; }
    public string? VaultId { get; set; }

    /// <summary>If a prior attempt already created the PayPal order, pass its id so the gateway skips CreateOrder and goes straight to AuthorizeOrder.</summary>
    public string? ExistingPayPalOrderId { get; set; }

    /// <summary>Stable base for this payment's idempotency keys; the gateway derives per-step PayPal-Request-Id values from it.</summary>
    public string RequestKeyBase { get; set; } = string.Empty;
}
