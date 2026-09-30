using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.PayPal;

public enum ReconciliationMatchState
{
    Matched,
    InPayPalNotInEShop,
    InEShopNotInPayPal
}

public record ReconciliationEntry(
    ReconciliationMatchState MatchState,
    int? EShopOrderId,
    string? PayPalTransactionId,
    decimal? Amount,
    string? Currency,
    string? Status,
    DateTimeOffset? Date,
    string Note);

public record ReconciliationReport(DateTimeOffset From, DateTimeOffset To, IReadOnlyList<ReconciliationEntry> Entries);
