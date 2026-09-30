using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.PayPal;

namespace Microsoft.eShopWeb.PublicApi.ReconciliationEndpoints;

public class ReconciliationEntryDto
{
    public string MatchState { get; set; } = default!;
    public int? EShopOrderId { get; set; }
    public string? PayPalTransactionId { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public string? Status { get; set; }
    public DateTimeOffset? Date { get; set; }
    public string Note { get; set; } = default!;
}

public static class ReconciliationDtoMapper
{
    public static List<ReconciliationEntryDto> FromDomain(IEnumerable<ReconciliationEntry> entries) =>
        entries.Select(e => new ReconciliationEntryDto
        {
            MatchState = e.MatchState.ToString(),
            EShopOrderId = e.EShopOrderId,
            PayPalTransactionId = e.PayPalTransactionId,
            Amount = e.Amount,
            Currency = e.Currency,
            Status = e.Status,
            Date = e.Date,
            Note = e.Note
        }).ToList();
}
