using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.PayPal;
using NSubstitute;
using PayPalServerSdk;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.PayPal;

public class PayPalTransactionReportReaderTests
{
    private static PayPalTransactionReportReader ReaderReturning(Func<HttpRequestMessage, HttpResponseMessage> responder, out StubHttpMessageHandler handler)
    {
        handler = new StubHttpMessageHandler(responder);
        var client = new PayPalServerSdkClient(new HttpClient(handler), new PayPalServerSdkClientOptions());
        return new PayPalTransactionReportReader(client, Substitute.For<IAppLogger<PayPalTransactionReportReader>>());
    }

    private static string Page(int page, int totalPages)
    {
        const string template =
            "{\"transaction_details\":[{\"transaction_info\":{\"transaction_id\":\"T__PAGE__\"," +
            "\"transaction_amount\":{\"currency_code\":\"USD\",\"value\":\"10.00\"},\"transaction_status\":\"S\"," +
            "\"transaction_initiation_date\":\"2026-09-01T00:00:00Z\"}}]," +
            "\"page\":__PAGE__,\"total_pages\":__TOTAL__,\"total_items\":1}";
        return template.Replace("__PAGE__", page.ToString()).Replace("__TOTAL__", totalPages.ToString());
    }

    [Fact]
    public async Task SearchAsync_splits_range_into_31_day_chunks()
    {
        // 40 days -> two chunks (31 + 9), each a single page.
        var reader = ReaderReturning(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, Page(1, 1)), out var handler);

        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var result = await reader.SearchAsync(from, from.AddDays(40), CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(2, result.Records.Count);
        Assert.False(result.Truncated);
    }

    [Fact]
    public async Task SearchAsync_pages_within_a_chunk()
    {
        // One chunk (<=31 days), two pages.
        var reader = ReaderReturning(req =>
        {
            var page = req.RequestUri!.Query.Contains("page=2") ? 2 : 1;
            return StubHttpMessageHandler.Json(HttpStatusCode.OK, Page(page, 2));
        }, out var handler);

        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var result = await reader.SearchAsync(from, from.AddDays(10), CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(2, result.Records.Count);
    }
}
