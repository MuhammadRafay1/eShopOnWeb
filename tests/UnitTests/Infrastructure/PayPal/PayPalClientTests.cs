using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.eShopWeb.Infrastructure.PayPal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.Infrastructure.PayPal;

public class PayPalClientTests
{
    private static PayPalClient BuildClient(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api-m.sandbox.paypal.com") };
        var tokenProvider = Substitute.For<IPayPalTokenProvider>();
        tokenProvider.GetAccessTokenAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult("test-token"));
        return new PayPalClient(httpClient, tokenProvider, NullLogger<PayPalClient>.Instance);
    }

    private static readonly CardDetails TestCard = new(
        "John Doe", "4111111111111111", "2030-01", "123",
        new CardBillingAddress("1 Main St", null, "San Jose", "CA", "95131", "US"));

    [Fact]
    public async Task AuthorizeWithCardSendsCorrectRequestAndParsesAuthorization()
    {
        const string response = """
        {
          "id": "PPORDER123",
          "status": "COMPLETED",
          "purchase_units": [
            { "payments": { "authorizations": [
              { "id": "AUTH123", "status": "CREATED", "expiration_time": "2030-01-15T00:00:00Z" }
            ] } }
          ]
        }
        """;
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.Created, response);
        var client = BuildClient(handler);

        var result = await client.AuthorizeWithCardAsync(42, "USD", 12.34m, TestCard);

        Assert.Equal("PPORDER123", result.PayPalOrderId);
        Assert.Equal("AUTH123", result.AuthorizationId);
        Assert.Equal("CREATED", result.Status);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v2/checkout/orders", request.RequestUri!.AbsolutePath);
        Assert.Equal("paypal-auth-42", request.Headers.GetValues("PayPal-Request-Id").Single());
        Assert.Equal("return=representation", request.Headers.GetValues("Prefer").Single());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);

        var body = handler.RequestBodies.Single();
        Assert.Contains("\"intent\":\"AUTHORIZE\"", body);
        Assert.Contains("\"custom_id\":\"42\"", body);
        Assert.Contains("\"value\":\"12.34\"", body);
        Assert.Contains("\"number\":\"4111111111111111\"", body);
    }

    [Fact]
    public async Task AuthorizeWithVaultedCardSendsVaultId()
    {
        const string response = """
        { "id": "PPORDER1", "status": "COMPLETED",
          "purchase_units": [ { "payments": { "authorizations": [
            { "id": "AUTH1", "status": "CREATED", "expiration_time": "2030-01-15T00:00:00Z" } ] } } ] }
        """;
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.Created, response);
        var client = BuildClient(handler);

        await client.AuthorizeWithVaultedCardAsync(7, "USD", 5m, "VAULT-XYZ");

        var body = handler.RequestBodies.Single();
        Assert.Contains("\"vault_id\":\"VAULT-XYZ\"", body);
        Assert.DoesNotContain("\"number\"", body);
    }

    [Fact]
    public async Task PayerActionRequiredThrows()
    {
        const string response = """
        { "id": "PPORDER1", "status": "PAYER_ACTION_REQUIRED",
          "links": [ { "rel": "payer-action", "href": "https://x", "method": "GET" } ] }
        """;
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.Created, response);
        var client = BuildClient(handler);

        await Assert.ThrowsAsync<PayPalPayerActionRequiredException>(
            () => client.AuthorizeWithCardAsync(1, "USD", 1m, TestCard));
    }

    [Fact]
    public async Task CaptureParsesSellerReceivableBreakdown()
    {
        const string response = """
        {
          "id": "CAP123", "status": "COMPLETED",
          "amount": { "currency_code": "USD", "value": "100.00" },
          "seller_receivable_breakdown": {
            "gross_amount": { "currency_code": "USD", "value": "100.00" },
            "paypal_fee": { "currency_code": "USD", "value": "3.20" },
            "net_amount": { "currency_code": "USD", "value": "96.80" }
          }
        }
        """;
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.Created, response);
        var client = BuildClient(handler);

        var result = await client.CaptureAsync("AUTH123", "paypal-capture-42");

        Assert.Equal("CAP123", result.CaptureId);
        Assert.Equal(100.00m, result.Amount);
        Assert.Equal(3.20m, result.PayPalFee);
        Assert.Equal(96.80m, result.NetAmount);

        var request = handler.Requests.Single();
        Assert.Equal("/v2/payments/authorizations/AUTH123/capture", request.RequestUri!.AbsolutePath);
        Assert.Equal("paypal-capture-42", request.Headers.GetValues("PayPal-Request-Id").Single());
        Assert.Contains("\"final_capture\":true", handler.RequestBodies.Single());
    }

    [Fact]
    public async Task RefundFullSendsEmptyBodyPartialSendsAmount()
    {
        const string refundResponse = """
        { "id": "REF1", "status": "COMPLETED", "amount": { "currency_code": "USD", "value": "25.00" } }
        """;
        var handler = new FakeHttpMessageHandler()
            .Enqueue(HttpStatusCode.Created, refundResponse)
            .Enqueue(HttpStatusCode.Created, refundResponse);
        var client = BuildClient(handler);

        await client.RefundAsync("CAP1", "USD", null, "paypal-refund-1-k1");
        await client.RefundAsync("CAP1", "USD", 25m, "paypal-refund-1-k2");

        Assert.Equal("{}", handler.RequestBodies[0]);
        Assert.Contains("\"value\":\"25.00\"", handler.RequestBodies[1]);
        Assert.Equal("/v2/payments/captures/CAP1/refund", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task NonSuccessResponseThrowsPayPalApiExceptionWithDebugId()
    {
        const string error = """
        { "name": "UNPROCESSABLE_ENTITY", "message": "Card denied", "debug_id": "abc123",
          "details": [ { "issue": "CARD_DECLINED", "description": "declined" } ] }
        """;
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.UnprocessableEntity, error);
        var client = BuildClient(handler);

        var ex = await Assert.ThrowsAsync<PayPalApiException>(
            () => client.CaptureAsync("AUTH1", "k"));
        Assert.Equal("abc123", ex.DebugId);
        Assert.Equal(422, ex.HttpStatus);
    }

    [Fact]
    public async Task CreatePaymentTokenParsesDisplayFields()
    {
        const string response = """
        { "id": "VAULT99", "status": "VAULTED",
          "payment_source": { "card": { "brand": "VISA", "last_digits": "1111", "expiry": "2030-01" } } }
        """;
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.Created, response);
        var client = BuildClient(handler);

        var vaulted = await client.CreatePaymentTokenAsync("SETUP1");

        Assert.Equal("VAULT99", vaulted.VaultId);
        Assert.Equal("VISA", vaulted.Brand);
        Assert.Equal("1111", vaulted.Last4Digits);
        var body = handler.RequestBodies.Single();
        Assert.Contains("\"id\":\"SETUP1\"", body);
        Assert.Contains("\"type\":\"SETUP_TOKEN\"", body);
    }

    [Fact]
    public async Task SearchTransactionsPagesThroughAllPages()
    {
        const string page1 = """
        { "transaction_details": [ { "transaction_info": {
            "transaction_id": "T1", "custom_field": "42",
            "transaction_amount": { "currency_code": "USD", "value": "10.00" },
            "transaction_status": "S" } } ],
          "page": 1, "total_items": 2, "total_pages": 2 }
        """;
        const string page2 = """
        { "transaction_details": [ { "transaction_info": {
            "transaction_id": "T2", "custom_field": "43",
            "transaction_amount": { "currency_code": "USD", "value": "20.00" },
            "transaction_status": "S" } } ],
          "page": 2, "total_items": 2, "total_pages": 2 }
        """;
        var handler = new FakeHttpMessageHandler()
            .Enqueue(HttpStatusCode.OK, page1)
            .Enqueue(HttpStatusCode.OK, page2);
        var client = BuildClient(handler);

        var txns = await client.SearchTransactionsAsync(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);

        Assert.Equal(2, txns.Count);
        Assert.Contains(txns, t => t.TransactionId == "T1");
        Assert.Contains(txns, t => t.TransactionId == "T2");
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("page=1", handler.Requests[0].RequestUri!.Query);
        Assert.Contains("page=2", handler.Requests[1].RequestUri!.Query);
    }
}
