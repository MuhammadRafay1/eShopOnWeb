using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Transaction Search v1 client. Handles the two contract constraints internally so callers pass a
/// range of any width: the 31-day-per-request maximum (the range is chunked) and pagination (every
/// page of every window is fetched). Built to transaction_search_v1.json.
/// </summary>
public class PayPalTransactionSearchClient : PayPalClientBase, IPayPalTransactionSearchClient
{
    // Spec: end_date "The maximum supported range is 31 days." Use 31 days minus a small margin.
    private static readonly TimeSpan MaxWindow = TimeSpan.FromDays(31);
    private const int PageSize = 500; // spec maximum

    public PayPalTransactionSearchClient(HttpClient httpClient, IPayPalAccessTokenProvider tokenProvider)
        : base(httpClient, tokenProvider)
    {
    }

    public async Task<IReadOnlyList<PayPalTransactionRecord>> SearchAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var records = new List<PayPalTransactionRecord>();
        if (to < from)
        {
            return records;
        }

        var windowStart = from;
        while (windowStart < to)
        {
            var windowEnd = windowStart + MaxWindow;
            if (windowEnd > to)
            {
                windowEnd = to;
            }

            await SearchWindowAsync(windowStart, windowEnd, records, cancellationToken);

            // Advance past the end of this window (avoid double-counting the boundary instant).
            windowStart = windowEnd.AddTicks(1);
        }

        return records;
    }

    private async Task SearchWindowAsync(
        DateTimeOffset start, DateTimeOffset end, List<PayPalTransactionRecord> sink, CancellationToken cancellationToken)
    {
        int page = 1;
        int totalPages;
        do
        {
            var url = "/v1/reporting/transactions"
                + $"?start_date={Uri.EscapeDataString(FormatDate(start))}"
                + $"&end_date={Uri.EscapeDataString(FormatDate(end))}"
                + "&fields=transaction_info"
                + $"&page_size={PageSize}"
                + $"&page={page}";

            var root = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, url), cancellationToken);
            if (root is null)
            {
                return;
            }

            totalPages = TryGetProperty(root.Value, "total_pages", out var tp) && tp.ValueKind == JsonValueKind.Number
                ? tp.GetInt32()
                : 1;

            if (TryGetProperty(root.Value, "transaction_details", out var details) && details.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in details.EnumerateArray())
                {
                    if (TryGetProperty(item, "transaction_info", out var info))
                    {
                        sink.Add(ParseTransaction(info));
                    }
                }
            }

            page++;
        }
        while (page <= totalPages);
    }

    private static string FormatDate(DateTimeOffset value)
        => value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private static PayPalTransactionRecord ParseTransaction(JsonElement info)
    {
        var record = new PayPalTransactionRecord
        {
            TransactionId = GetString(info, "transaction_id"),
            InvoiceId = GetString(info, "invoice_id"),
            CustomField = GetString(info, "custom_field"),
            PayPalReferenceId = GetString(info, "paypal_reference_id"),
            PayPalReferenceIdType = GetString(info, "paypal_reference_id_type"),
            EventCode = GetString(info, "transaction_event_code"),
            Status = GetString(info, "transaction_status"),
            InitiationDate = GetDateTime(info, "transaction_initiation_date"),
            UpdatedDate = GetDateTime(info, "transaction_updated_date")
        };

        if (TryGetProperty(info, "transaction_amount", out var amount))
        {
            record.Amount = GetMoneyValue(amount);
            record.CurrencyCode = GetString(amount, "currency_code");
        }
        if (TryGetProperty(info, "fee_amount", out var fee))
        {
            record.FeeAmount = GetMoneyValue(fee);
        }

        return record;
    }
}
