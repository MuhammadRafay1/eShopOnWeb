using System;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// A per-process token mixed into PayPal idempotency keys (PayPal-Request-Id) and the invoice_id.
///
/// Two constraints motivate this: (1) the sandbox merchant account rejects a duplicate invoice_id
/// across transactions, and (2) with the in-memory database order ids restart at 1 on every run, so
/// a purely order-id-based invoice_id / request-id would collide with a previous run's values (and
/// PayPal caches request-id keys for hours). Scoping those keys to the run keeps them idempotent
/// WITHIN a run (a double-click on the same order reuses the same key and PayPal de-duplicates)
/// while staying unique ACROSS runs. In a persistent-database deployment order ids are already
/// unique, and this token simply makes the keys unique per deployment/run too.
/// </summary>
public static class PayPalRequestContext
{
    public static readonly string RunToken = Guid.NewGuid().ToString("N").Substring(0, 12);
}
