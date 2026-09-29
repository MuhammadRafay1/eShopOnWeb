using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using Microsoft.eShopWeb.Infrastructure.Payments.PayPal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.Infrastructure.PayPal;

public class PayPalOrdersGatewayTests
{
    private static PayPalOrdersGateway BuildGateway(FakePayPalHandler handler)
    {
        var apiClient = new PayPalApiClient(
            new FakeHttpClientFactory(handler),
            new StubTokenProvider(),
            NullLogger<PayPalApiClient>.Instance);
        return new PayPalOrdersGateway(apiClient);
    }

    [Fact]
    public async Task AuthorizeParsesAuthorizationIdAndStatus()
    {
        const string body = """
        {
          "id": "PPO-123",
          "status": "COMPLETED",
          "purchase_units": [
            { "payments": { "authorizations": [
              { "id": "AUTH-999", "status": "CREATED", "expiration_time": "2030-01-01T00:00:00Z" }
            ] } }
          ]
        }
        """;
        var handler = new FakePayPalHandler().When(HttpMethod.Post, "v2/checkout/orders", HttpStatusCode.Created, body);
        var gateway = BuildGateway(handler);

        var outcome = await gateway.AuthorizeAsync(new PayPalAuthorizeRequest
        {
            Amount = 19.99m,
            Currency = "USD",
            InvoiceId = "42",
            IdempotencyKey = "authorize:42:1",
            Card = new CardDetails("4111111111111111", "2030-01", "123", "Test", null)
        }, CancellationToken.None);

        Assert.True(outcome.Approved);
        Assert.Equal("PPO-123", outcome.PayPalOrderId);
        Assert.Equal("AUTH-999", outcome.AuthorizationId);
        Assert.Equal(PaymentAuthorizationStatus.Created, outcome.Status);
    }

    [Fact]
    public async Task AuthorizeSerializesAmountToTwoDecimalsInvariant()
    {
        var handler = new FakePayPalHandler().When(HttpMethod.Post, "v2/checkout/orders",
            HttpStatusCode.Created,
            """{"id":"PPO","purchase_units":[{"payments":{"authorizations":[{"id":"A","status":"CREATED"}]}}]}""");
        var gateway = BuildGateway(handler);

        await gateway.AuthorizeAsync(new PayPalAuthorizeRequest
        {
            Amount = 1234.5m,
            Currency = "USD",
            InvoiceId = "1",
            IdempotencyKey = "k",
            Card = new CardDetails("4111111111111111", "2030-01", "123", "Test", null)
        }, CancellationToken.None);

        var sent = handler.RequestBodies[0];
        Assert.Contains("\"value\":\"1234.50\"", sent);
        Assert.Contains("\"intent\":\"AUTHORIZE\"", sent);
    }

    [Fact]
    public async Task AuthorizeWithVaultIdSendsStoredCredential()
    {
        var handler = new FakePayPalHandler().When(HttpMethod.Post, "v2/checkout/orders",
            HttpStatusCode.Created,
            """{"id":"PPO","purchase_units":[{"payments":{"authorizations":[{"id":"A","status":"CREATED"}]}}]}""");
        var gateway = BuildGateway(handler);

        await gateway.AuthorizeAsync(new PayPalAuthorizeRequest
        {
            Amount = 10m,
            Currency = "USD",
            InvoiceId = "1",
            IdempotencyKey = "k",
            VaultId = "VAULT-1"
        }, CancellationToken.None);

        var sent = handler.RequestBodies[0];
        Assert.Contains("\"vault_id\":\"VAULT-1\"", sent);
        Assert.Contains("\"stored_credential\"", sent);
        Assert.Contains("\"payment_initiator\":\"CUSTOMER\"", sent);
        Assert.Contains("\"usage\":\"SUBSEQUENT\"", sent);
        Assert.DoesNotContain("\"number\"", sent);
    }

    [Fact]
    public async Task AuthorizeReturnsDeclineOnClientError()
    {
        const string body = """{"name":"UNPROCESSABLE_ENTITY","details":[{"issue":"INSTRUMENT_DECLINED","description":"The instrument was declined."}]}""";
        var handler = new FakePayPalHandler().When(HttpMethod.Post, "v2/checkout/orders", HttpStatusCode.UnprocessableEntity, body);
        var gateway = BuildGateway(handler);

        var outcome = await gateway.AuthorizeAsync(new PayPalAuthorizeRequest
        {
            Amount = 10m,
            Currency = "USD",
            InvoiceId = "1",
            IdempotencyKey = "k",
            Card = new CardDetails("4000000000000002", "2030-01", "123", "Test", null)
        }, CancellationToken.None);

        Assert.False(outcome.Approved);
        Assert.Contains("INSTRUMENT_DECLINED", outcome.DeclineReason);
    }

    [Fact]
    public async Task AuthorizePayerActionRequiredIsHardStop()
    {
        const string body = """{"id":"PPO","status":"PAYER_ACTION_REQUIRED"}""";
        var handler = new FakePayPalHandler().When(HttpMethod.Post, "v2/checkout/orders", HttpStatusCode.Created, body);
        var gateway = BuildGateway(handler);

        await Assert.ThrowsAsync<PayPalApprovalRequiredException>(() =>
            gateway.AuthorizeAsync(new PayPalAuthorizeRequest
            {
                Amount = 10m,
                Currency = "USD",
                InvoiceId = "1",
                IdempotencyKey = "k",
                Card = new CardDetails("4111111111111111", "2030-01", "123", "Test", null)
            }, CancellationToken.None));
    }

    [Fact]
    public async Task CaptureParsesSellerReceivableBreakdown()
    {
        const string body = """
        {
          "id": "CAP-1",
          "status": "COMPLETED",
          "seller_receivable_breakdown": {
            "gross_amount": { "currency_code": "USD", "value": "50.00" },
            "paypal_fee": { "currency_code": "USD", "value": "2.05" },
            "net_amount": { "currency_code": "USD", "value": "47.95" }
          }
        }
        """;
        var handler = new FakePayPalHandler().When(HttpMethod.Post, "/capture", HttpStatusCode.Created, body);
        var gateway = BuildGateway(handler);

        var outcome = await gateway.CaptureAsync("AUTH-1", 50m, "USD", "42", "capture:42:0", CancellationToken.None);

        Assert.Equal(PayPalCaptureResult.Completed, outcome.Result);
        Assert.Equal("CAP-1", outcome.CaptureId);
        Assert.Equal(50m, outcome.GrossAmount);
        Assert.Equal(2.05m, outcome.FeeAmount);
        Assert.Equal(47.95m, outcome.NetAmount);
    }

    [Fact]
    public async Task CaptureExpiredAuthorizationIsDetected()
    {
        const string body = """{"name":"UNPROCESSABLE_ENTITY","details":[{"issue":"AUTHORIZATION_EXPIRED","description":"The authorization has expired."}]}""";
        var handler = new FakePayPalHandler().When(HttpMethod.Post, "/capture", HttpStatusCode.UnprocessableEntity, body);
        var gateway = BuildGateway(handler);

        var outcome = await gateway.CaptureAsync("AUTH-1", 50m, "USD", "42", "capture:42:0", CancellationToken.None);

        Assert.Equal(PayPalCaptureResult.AuthorizationExpired, outcome.Result);
        Assert.Equal("AUTHORIZATION_EXPIRED", outcome.FailureIssue);
    }

    [Fact]
    public async Task ReauthorizeParsesRenewedAuthorization()
    {
        const string body = """{"id":"AUTH-1","status":"CREATED","expiration_time":"2030-02-01T00:00:00Z"}""";
        var handler = new FakePayPalHandler().When(HttpMethod.Post, "/reauthorize", HttpStatusCode.Created, body);
        var gateway = BuildGateway(handler);

        var outcome = await gateway.ReauthorizeAsync("AUTH-1", 50m, "USD", CancellationToken.None);

        Assert.Equal(PayPalReauthorizeResult.Renewed, outcome.Result);
        Assert.Equal(PaymentAuthorizationStatus.Created, outcome.Status);
    }

    [Fact]
    public async Task RefundParsesResponse()
    {
        const string body = """{"id":"REF-1","status":"COMPLETED"}""";
        var handler = new FakePayPalHandler().When(HttpMethod.Post, "/refund", HttpStatusCode.Created, body);
        var gateway = BuildGateway(handler);

        var outcome = await gateway.RefundAsync("CAP-1", 10m, "USD", "idem-1", CancellationToken.None);

        Assert.Equal("REF-1", outcome.RefundId);
        Assert.Equal("COMPLETED", outcome.Status);

        // The caller's idempotency key is forwarded as PayPal-Request-Id.
        Assert.True(handler.Requests[0].Headers.TryGetValues("PayPal-Request-Id", out var vals));
        Assert.Contains("idem-1", vals!);
    }
}
