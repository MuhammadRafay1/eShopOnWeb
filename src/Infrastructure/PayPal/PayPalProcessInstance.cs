using System;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// A per-process identifier mixed into PayPal-Request-Id/invoice_id values so they stay unique
/// across restarts of the in-memory database (which resets order ids to 1 each run).
/// </summary>
public class PayPalProcessInstance
{
    public string InstanceId { get; } = Guid.NewGuid().ToString("N")[..8];
}
