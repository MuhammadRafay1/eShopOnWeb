using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;

namespace Microsoft.eShopWeb.Infrastructure.Payments.PayPal;

/// <summary>
/// Transaction search against PayPal Reporting v1. PayPal caps a single request at a 31-day range
/// and 500 items per page, so this transparently walks consecutive &lt;=31-day windows and paginates
/// fully within each, concatenating everything - the caller gets the whole requested range.
/// </summary>
public class PayPalReconciliationGateway : IPayPalReconciliationGateway
{
    private const int PageSize = 500;
    private static readonly TimeSpan MaxWindow = TimeSpan.FromDays(31);
    private readonly PayPalApiClient _api;

    public PayPalReconciliationGateway(PayPalApiClient api)
    {
        _api = api;
    }

    public async Task<IReadOnlyList<PayPalTransactionRecord>> SearchTransactionsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var results = new List<PayPalTransactionRecord>();

        var windowStart = from;
        while (windowStart < to)
        {
            // PayPal's limit is strictly "less than or equal to 31 days"; keep each window within it.
            var windowEnd = windowStart + MaxWindow;
            if (windowEnd > to)
            {
                windowEnd = to;
            }

            await ReadWindowAsync(windowStart, windowEnd, results, ct);

            windowStart = windowEnd;
        }

        return results;
    }

    private async Task ReadWindowAsync(DateTimeOffset start, DateTimeOffset end,
        List<PayPalTransactionRecord> results, CancellationToken ct)
    {
        var page = 1;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var path = "v1/reporting/transactions" +
                $"?start_date={Uri.EscapeDataString(FormatDate(start))}" +
                $"&end_date={Uri.EscapeDataString(FormatDate(end))}" +
                $"&fields=transaction_info&page_size={PageSize}&page={page}";

            var response = await _api.GetAsync<TransactionSearchResponseWire>(path, ct);
            if (!response.IsSuccess)
            {
                // Treat a 4xx here as an integration problem worth surfacing (bad range/format),
                // not a silent empty result.
                throw new PayPalIntegrationException(
                    $"PayPal transaction search failed ({(int)response.StatusCode}): {response.Error?.Describe()}");
            }

            var body = response.Value;
            var details = body?.TransactionDetails;
            if (details is null || details.Count == 0)
            {
                break;
            }

            foreach (var detail in details)
            {
                var info = detail.TransactionInfo;
                if (info is null)
                {
                    continue;
                }
                results.Add(new PayPalTransactionRecord
                {
                    TransactionId = info.TransactionId ?? string.Empty,
                    InvoiceId = string.IsNullOrWhiteSpace(info.InvoiceId) ? null : info.InvoiceId,
                    Amount = ParseMoney(info.TransactionAmount?.Value),
                    CurrencyCode = info.TransactionAmount?.CurrencyCode,
                    Status = info.TransactionStatus,
                    InitiationDate = info.TransactionInitiationDate ?? default,
                    FeeAmount = info.FeeAmount?.Value is null ? null : ParseMoney(info.FeeAmount.Value)
                });
            }

            var totalItems = body!.TotalItems ?? 0;
            if (page * PageSize >= totalItems)
            {
                break;
            }
            page++;
        }
    }

    // RFC 3339 with a numeric offset, which PayPal's reporting API requires.
    private static string FormatDate(DateTimeOffset dt) =>
        dt.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

    private static decimal ParseMoney(string? value)
    {
        if (value is null)
        {
            return 0m;
        }
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0m;
    }
}
