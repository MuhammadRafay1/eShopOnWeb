using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Payments;
using PayPalServerSdk;
using PayPalServerSdk.Core.ErrorResponse;
using PayPalServerSdk.Core.Exceptions;
using PayPalServerSdk.Requests.TransactionSearch;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Reads PayPal's transaction report via TransactionSearch. The provider caps a single query at a 31-day range,
/// so the full <c>[from, to]</c> is split into ≤31-day chunks, each paged by hand. Two provider-independent
/// backstops bound the work (a per-chunk page cap and a chunk cap); hitting either sets <c>Truncated</c>.
/// </summary>
public sealed class PayPalTransactionReportReader : ITransactionReportReader
{
    private static readonly TimeSpan CallBudget = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ChunkSize = TimeSpan.FromDays(31);
    private const int PageSize = 100;
    private const int MaxPagesPerChunk = 50;   // backstop, independent of TotalPages
    private const int MaxChunks = 24;          // ~2 years at 31 days/chunk

    private readonly PayPalServerSdkClient _client;
    private readonly IAppLogger<PayPalTransactionReportReader> _logger;

    public PayPalTransactionReportReader(PayPalServerSdkClient client, IAppLogger<PayPalTransactionReportReader> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<TransactionSearchResult> SearchAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var records = new List<TransactionRecord>();
        var truncated = false;
        DateTimeOffset? truncatedAfter = null;

        var chunkStart = from;
        var chunks = 0;

        while (chunkStart < to)
        {
            if (chunks >= MaxChunks)
            {
                truncated = true;
                truncatedAfter = chunkStart;
                break;
            }

            var chunkEnd = chunkStart + ChunkSize;
            if (chunkEnd > to)
            {
                chunkEnd = to;
            }

            var chunkTruncated = await ReadChunkAsync(chunkStart, chunkEnd, records, cancellationToken);
            if (chunkTruncated)
            {
                truncated = true;
                truncatedAfter = chunkStart;
                break;
            }

            chunkStart = chunkEnd;
            chunks++;
        }

        return new TransactionSearchResult(records, truncated, truncatedAfter);
    }

    private async Task<bool> ReadChunkAsync(DateTimeOffset start, DateTimeOffset end, List<TransactionRecord> sink, CancellationToken cancellationToken)
    {
        var page = 1;
        while (true)
        {
            var request = new SearchTransactionsRequest
            {
                StartDate = Format(start),
                EndDate = Format(end),
                Fields = "transaction_info",
                PageSize = PageSize,
                Page = page
            };

            SearchResponseResult response;
            try
            {
                var raw = await Bounded(ct => _client.TransactionSearch.SearchTransactions(request, cancellationToken: ct), cancellationToken);
                response = new SearchResponseResult(raw.TransactionDetails, raw.TotalPages ?? 1);
            }
            catch (ApiException<RawError> ex)
            {
                _logger.LogWarning("Transaction search failed (HTTP {0}) for page {1}.", (int)ex.StatusCode, page);
                throw new PaymentGatewayException($"PayPal transaction search failed (HTTP {(int)ex.StatusCode}).", (int)ex.StatusCode, inner: ex);
            }
            catch (Exception ex) when (ex is ResponseDeserializationException or SdkTimeoutException or SdkConnectionException or AuthSchemeException)
            {
                throw Translate(ex);
            }

            if (response.Details is not null)
            {
                foreach (var detail in response.Details)
                {
                    var info = detail.TransactionInfo;
                    if (info is null)
                    {
                        continue;
                    }
                    sink.Add(new TransactionRecord(
                        info.TransactionId,
                        MoneyFormatter.ParseOrNull(info.TransactionAmount?.Value),
                        info.TransactionAmount?.CurrencyCode,
                        info.TransactionStatus,
                        ParseDate(info.TransactionInitiationDate)));
                }
            }

            if (page >= response.TotalPages)
            {
                return false; // chunk complete
            }

            page++;
            if (page > MaxPagesPerChunk)
            {
                return true; // chunk truncated by the page cap
            }
        }
    }

    private static string Format(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static DateTimeOffset? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : null;

    private PaymentGatewayException Translate(Exception ex) => ex switch
    {
        ResponseDeserializationException rde => new PaymentGatewayException("PayPal returned a response that could not be processed.", (int)rde.StatusCode, inner: rde),
        SdkTimeoutException te => new PaymentGatewayException("PayPal did not respond in time.", null, inner: te),
        SdkConnectionException ce => new PaymentGatewayException("PayPal is currently unreachable.", null, inner: ce),
        AuthSchemeException ae => new PaymentGatewayException("PayPal credentials were rejected.", null, inner: ae),
        _ => new PaymentGatewayException("An unexpected PayPal error occurred.", null, inner: ex)
    };

    private static async Task<T> Bounded<T>(Func<CancellationToken, Task<T>> call, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(CallBudget);
        return await call(cts.Token);
    }

    private sealed record SearchResponseResult(IReadOnlyList<PayPalServerSdk.Models.TransactionDetails>? Details, int TotalPages);
}
