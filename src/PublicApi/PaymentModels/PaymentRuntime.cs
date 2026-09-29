using System;

namespace Microsoft.eShopWeb.PublicApi.PaymentModels;

/// <summary>
/// Process-lifetime constants for payment references. RunId disambiguates invoice ids across
/// process restarts - important because the in-memory database restarts order ids from 1 each run,
/// while PayPal can require invoice ids to be globally unique across the account's history.
/// </summary>
public static class PaymentRuntime
{
    public static readonly string RunId = Guid.NewGuid().ToString("N").Substring(0, 8);

    /// <summary>
    /// Builds a unique-but-order-traceable invoice reference for a given order/attempt. Stable for
    /// a given (order, attempt) within a run so an idempotent retry sends an identical body, and
    /// distinct per attempt/run so PayPal never sees a duplicate invoice id.
    /// </summary>
    public static string BuildInvoiceId(int orderId, int attempt) =>
        $"eshop-{orderId}-{RunId}-{attempt}";

    /// <summary>
    /// PayPal-Request-Id for an authorize attempt. Includes RunId so it is stable within a run
    /// (a double-click of the same attempt dedupes) but unique across runs - important because
    /// PayPal caches request ids for hours while the in-memory database restarts order ids from 1.
    /// </summary>
    public static string BuildAuthorizeKey(int orderId, int attempt) =>
        $"authorize:{RunId}:{orderId}:{attempt}";

    /// <summary>PayPal-Request-Id for a capture at a given reauthorization count (run-unique).</summary>
    public static string BuildCaptureKey(int orderId, int reauthorizationCount) =>
        $"capture:{RunId}:{orderId}:{reauthorizationCount}";
}
