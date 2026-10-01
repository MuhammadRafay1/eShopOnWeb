using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Services;
using PayPalServerSdk;
using Xunit;

namespace Microsoft.eShopWeb.IntegrationTests.Infrastructure;

public class PayPalPaymentGatewayTests
{
    private static PayPalPaymentGateway GatewayFor(StubHandler handler)
    {
        var client = new PayPalServerSdkClient(new HttpClient(handler), new PayPalServerSdkClientOptions());
        return new PayPalPaymentGateway(client);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
    };

    [Fact]
    public async Task CreateOrderAndAuthorizeAsync_SingleStepAuthorization_MapsResult()
    {
        // CreateOrder returns the order already carrying an authorization (single-step outcome).
        const string body = """
        {
          "id": "PAYPAL-ORDER-1",
          "status": "COMPLETED",
          "purchase_units": [
            {
              "payments": {
                "authorizations": [
                  { "id": "AUTH-1", "status": "CREATED", "amount": { "currency_code": "USD", "value": "12.34" }, "expiration_time": "2026-01-01T00:00:00Z" }
                ]
              }
            }
          ]
        }
        """;
        var handler = new StubHandler(_ => Json(HttpStatusCode.Created, body));
        var gateway = GatewayFor(handler);

        var result = await gateway.CreateOrderAndAuthorizeAsync(
            new PayPalAuthorizeOrderRequest(1, 12.34m, "USD", "req-1", new PayPalCardInput("4111111111111111", "2030-01", "123", "Jane Doe", null, null, null, null, null, "US"), null),
            CancellationToken.None);

        Assert.Equal("PAYPAL-ORDER-1", result.PayPalOrderId);
        Assert.Equal("AUTH-1", result.Authorization.AuthorizationId);
        Assert.Equal("CREATED", result.Authorization.Status);
        Assert.Equal(12.34m, result.Authorization.Amount);
        Assert.NotNull(result.Authorization.ExpiresAt);

        // Only one call: CreateOrder. No separate AuthorizeOrder needed when the authorization already came back.
        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Contains("\"purchase_units\"", handler.LastBody);
        Assert.Contains("\"value\":\"12.34\"", handler.LastBody);
    }

    [Fact]
    public async Task CreateOrderAndAuthorizeAsync_TwoStep_CallsAuthorizeOrder()
    {
        const string createBody = """{ "id": "PAYPAL-ORDER-2", "status": "CREATED", "purchase_units": [ { "payments": {} } ] }""";
        const string authorizeBody = """
        {
          "id": "PAYPAL-ORDER-2",
          "status": "COMPLETED",
          "purchase_units": [
            { "payments": { "authorizations": [ { "id": "AUTH-2", "status": "CREATED", "amount": { "currency_code": "USD", "value": "5.00" } } ] } }
          ]
        }
        """;
        var handler = new StubHandler(new Func<HttpRequestMessage, HttpResponseMessage>[]
        {
            _ => Json(HttpStatusCode.Created, createBody),
            _ => Json(HttpStatusCode.Created, authorizeBody)
        });
        var gateway = GatewayFor(handler);

        var result = await gateway.CreateOrderAndAuthorizeAsync(
            new PayPalAuthorizeOrderRequest(2, 5.00m, "USD", "req-2", null, "VAULT-ID-1"),
            CancellationToken.None);

        Assert.Equal("AUTH-2", result.Authorization.AuthorizationId);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("authorize", handler.Requests[1].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CreateOrderAndAuthorizeAsync_PayerActionRequired_ThrowsChallengeRequired()
    {
        const string body = """{ "id": "PAYPAL-ORDER-3", "status": "PAYER_ACTION_REQUIRED" }""";
        var handler = new StubHandler(_ => Json(HttpStatusCode.Created, body));
        var gateway = GatewayFor(handler);

        var ex = await Assert.ThrowsAsync<PaymentGatewayException>(() => gateway.CreateOrderAndAuthorizeAsync(
            new PayPalAuthorizeOrderRequest(3, 1m, "USD", "req-3", new PayPalCardInput("4111111111111111", "2030-01", null, null, null, null, null, null, null, null), null),
            CancellationToken.None));

        Assert.Equal(PaymentGatewayFailureReason.ChallengeRequired, ex.Reason);
    }

    [Fact]
    public async Task CreateOrderAndAuthorizeAsync_TypedApiError_MapsToProviderRejected()
    {
        const string body = """
        {
          "name": "UNPROCESSABLE_ENTITY",
          "message": "The requested action could not be performed.",
          "debug_id": "debug-123",
          "details": [ { "issue": "DECLINED" } ]
        }
        """;
        var handler = new StubHandler(_ => Json(HttpStatusCode.UnprocessableEntity, body));
        var gateway = GatewayFor(handler);

        var ex = await Assert.ThrowsAsync<PaymentGatewayException>(() => gateway.CreateOrderAndAuthorizeAsync(
            new PayPalAuthorizeOrderRequest(4, 1m, "USD", "req-4", new PayPalCardInput("4111111111111111", "2030-01", null, null, null, null, null, null, null, null), null),
            CancellationToken.None));

        Assert.Equal(PaymentGatewayFailureReason.ProviderRejected, ex.Reason);
        Assert.Equal("debug-123", ex.ProviderDebugId);
        Assert.Contains("could not be performed", ex.Message);
    }

    [Fact]
    public async Task CreateOrderAndAuthorizeAsync_ConnectionFailureThenSuccess_SettlesWithoutDuplicateSend()
    {
        const string successBody = """
        {
          "id": "PAYPAL-ORDER-5",
          "status": "COMPLETED",
          "purchase_units": [ { "payments": { "authorizations": [ { "id": "AUTH-5", "status": "CREATED", "amount": { "currency_code": "USD", "value": "9.00" } } ] } } ]
        }
        """;
        var handler = new StubHandler(new Func<HttpRequestMessage, HttpResponseMessage>[]
        {
            _ => throw new HttpRequestException("connection reset"),
            _ => Json(HttpStatusCode.Created, successBody)
        });
        var gateway = GatewayFor(handler);

        var result = await gateway.CreateOrderAndAuthorizeAsync(
            new PayPalAuthorizeOrderRequest(5, 9.00m, "USD", "req-5", new PayPalCardInput("4111111111111111", "2030-01", null, null, null, null, null, null, null, null), null),
            CancellationToken.None);

        Assert.Equal("AUTH-5", result.Authorization.AuthorizationId);
        // Exactly one retry - the gateway settles the unknown outcome itself rather than leaving it to the caller.
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task CreateOrderAndAuthorizeAsync_ConnectionFailureTwice_SurfacesUnavailable()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("connection reset"));
        var gateway = GatewayFor(handler);

        var ex = await Assert.ThrowsAsync<PaymentGatewayException>(() => gateway.CreateOrderAndAuthorizeAsync(
            new PayPalAuthorizeOrderRequest(6, 1m, "USD", "req-6", new PayPalCardInput("4111111111111111", "2030-01", null, null, null, null, null, null, null, null), null),
            CancellationToken.None));

        Assert.Equal(PaymentGatewayFailureReason.Unavailable, ex.Reason);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task VoidAsync_204NoContent_IsTreatedAsSuccess()
    {
        // Verified against the live sandbox: VoidPayment answers 204 No Content on success, even though
        // its declared return type is PaymentAuthorization.
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var gateway = GatewayFor(handler);

        await gateway.VoidAsync("AUTH-1", "void-req-1", CancellationToken.None);

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task VoidAsync_TypedApiError_StillSurfacesAsProviderRejected()
    {
        const string body = """{ "name": "UNPROCESSABLE_ENTITY", "message": "Authorization already captured.", "debug_id": "debug-1" }""";
        var handler = new StubHandler(_ => Json(HttpStatusCode.UnprocessableEntity, body));
        var gateway = GatewayFor(handler);

        var ex = await Assert.ThrowsAsync<PaymentGatewayException>(() => gateway.VoidAsync("AUTH-1", "void-req-2", CancellationToken.None));

        Assert.Equal(PaymentGatewayFailureReason.ProviderRejected, ex.Reason);
    }

    [Fact]
    public async Task CaptureAsync_MapsSellerReceivableBreakdown()
    {
        const string body = """
        {
          "id": "CAP-1",
          "status": "COMPLETED",
          "amount": { "currency_code": "USD", "value": "20.00" },
          "seller_receivable_breakdown": {
            "gross_amount": { "currency_code": "USD", "value": "20.00" },
            "paypal_fee": { "currency_code": "USD", "value": "1.18" },
            "net_amount": { "currency_code": "USD", "value": "18.82" }
          }
        }
        """;
        var handler = new StubHandler(_ => Json(HttpStatusCode.Created, body));
        var gateway = GatewayFor(handler);

        var result = await gateway.CaptureAsync("AUTH-1", "capture-req-1", CancellationToken.None);

        Assert.Equal("CAP-1", result.CaptureId);
        Assert.Equal("COMPLETED", result.Status);
        Assert.Equal(20.00m, result.GrossAmount);
        Assert.Equal(1.18m, result.FeeAmount);
        Assert.Equal(18.82m, result.NetAmount);
    }

    [Fact]
    public async Task VaultCardAsync_MapsCardDescriptor_AndNeverSendsOutsideTheOneRequest()
    {
        const string body = """
        {
          "id": "VAULT-1",
          "customer": { "id": "CUST-1" },
          "payment_source": {
            "card": { "brand": "VISA", "last_digits": "1111", "expiry": "2030-01", "name": "Jane Doe" }
          }
        }
        """;
        var handler = new StubHandler(_ => Json(HttpStatusCode.Created, body));
        var gateway = GatewayFor(handler);

        var card = new PayPalCardInput("4111111111111111", "2030-01", "123", "Jane Doe", null, null, null, null, null, "US");
        var result = await gateway.VaultCardAsync(new PayPalVaultCardRequest(card, null, "vault-req-1"), CancellationToken.None);

        Assert.Equal("VAULT-1", result.VaultId);
        Assert.Equal("CUST-1", result.CustomerId);
        Assert.Equal("VISA", result.Brand);
        Assert.Equal("1111", result.LastDigits);
        Assert.Equal("2030-01", result.Expiry);
        Assert.Single(handler.Requests);
        Assert.Contains("4111111111111111", handler.LastBody);
    }

    [Fact]
    public async Task ReauthorizeAsync_ExpiredAuthorizationError_MapsToReauthorizationExpired()
    {
        const string body = """
        {
          "name": "UNPROCESSABLE_ENTITY",
          "message": "The requested action could not be performed, semantically incorrect, or failed business validation.",
          "debug_id": "debug-999",
          "details": [ { "issue": "AUTHORIZATION_EXPIRED", "description": "The requested action could not be performed because the authorization has expired." } ]
        }
        """;
        var handler = new StubHandler(_ => Json(HttpStatusCode.UnprocessableEntity, body));
        var gateway = GatewayFor(handler);

        var ex = await Assert.ThrowsAsync<PaymentGatewayException>(() =>
            gateway.ReauthorizeAsync("AUTH-1", 10m, "USD", "reauth-req-1", CancellationToken.None));

        Assert.Equal(PaymentGatewayFailureReason.ReauthorizationExpired, ex.Reason);
    }

    [Fact]
    public async Task SearchTransactionsAsync_PagesUntilTotalPagesReached()
    {
        string Page(int page, int totalPages, string transactionId) => $$"""
        {
          "transaction_details": [ { "transaction_info": { "transaction_id": "{{transactionId}}", "transaction_amount": { "currency_code": "USD", "value": "1.00" }, "invoice_id": "ESHOP-1" } } ],
          "total_pages": {{totalPages}}
        }
        """;
        var handler = new StubHandler(new Func<HttpRequestMessage, HttpResponseMessage>[]
        {
            _ => Json(HttpStatusCode.OK, Page(1, 2, "T1")),
            _ => Json(HttpStatusCode.OK, Page(2, 2, "T2"))
        });
        var gateway = GatewayFor(handler);

        var result = await gateway.SearchTransactionsAsync(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.True(result.Complete);
        Assert.Equal(2, result.Transactions.Count);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(new[] { "T1", "T2" }, result.Transactions.Select(t => t.TransactionId));
    }
}
