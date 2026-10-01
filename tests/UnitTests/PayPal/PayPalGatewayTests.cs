using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Payments;
using Microsoft.eShopWeb.Infrastructure.PayPal;
using NSubstitute;
using PayPalServerSdk;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.PayPal;

public class PayPalGatewayTests
{
    private static PayPalGateway GatewayReturning(System.Func<HttpRequestMessage, HttpResponseMessage> responder, out StubHttpMessageHandler handler)
    {
        handler = new StubHttpMessageHandler(responder);
        // No Oauth2 configured => the SDK makes no token call, so the stub only sees the API request.
        var client = new PayPalServerSdkClient(new HttpClient(handler), new PayPalServerSdkClientOptions());
        return new PayPalGateway(client, Substitute.For<IAppLogger<PayPalGateway>>());
    }

    private static PaymentAuthorizationRequest AuthRequest() => new(
        "order-1",
        new Money(29m, "USD"),
        new CardDetails("Test", "4111111111111111", "2030-01", "123", new CardBillingAddress(null, null, null, null, null, "US")),
        null);

    [Fact]
    public async Task AuthorizeAsync_returns_authorization_from_create_order()
    {
        const string json = """
        {"id":"ORDER1","status":"COMPLETED","purchase_units":[{"payments":{"authorizations":[
        {"id":"AUTH1","status":"CREATED","expiration_time":"2026-10-30T10:00:00Z"}]}}]}
        """;
        var gateway = GatewayReturning(_ => StubHttpMessageHandler.Json(HttpStatusCode.Created, json), out _);

        var result = await gateway.AuthorizeAsync(AuthRequest(), CancellationToken.None);

        Assert.Equal("ORDER1", result.PayPalOrderId);
        Assert.Equal("AUTH1", result.AuthorizationId);
        Assert.Equal("CREATED", result.AuthorizationStatus);
        Assert.NotNull(result.ExpiresAt);
    }

    [Fact]
    public async Task AuthorizeAsync_throws_PayerActionRequired_on_challenge()
    {
        const string json = """{"id":"ORDER1","status":"PAYER_ACTION_REQUIRED","purchase_units":[]}""";
        var gateway = GatewayReturning(_ => StubHttpMessageHandler.Json(HttpStatusCode.Created, json), out _);

        await Assert.ThrowsAsync<PayerActionRequiredException>(
            () => gateway.AuthorizeAsync(AuthRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task AuthorizeAsync_maps_422_to_gateway_exception_with_status()
    {
        const string json = """{"name":"UNPROCESSABLE_ENTITY","message":"declined","debug_id":"d1"}""";
        var gateway = GatewayReturning(_ => StubHttpMessageHandler.Json(HttpStatusCode.UnprocessableEntity, json), out _);

        var ex = await Assert.ThrowsAsync<PaymentGatewayException>(
            () => gateway.AuthorizeAsync(AuthRequest(), CancellationToken.None));
        Assert.Equal(422, ex.StatusCode);
        Assert.Equal("d1", ex.ProviderDebugId);
    }

    [Fact]
    public async Task CaptureAsync_returns_fee_and_net_from_breakdown()
    {
        const string json = """
        {"id":"CAP1","status":"COMPLETED","amount":{"currency_code":"USD","value":"29.00"},
        "seller_receivable_breakdown":{"gross_amount":{"currency_code":"USD","value":"29.00"},
        "paypal_fee":{"currency_code":"USD","value":"1.24"},"net_amount":{"currency_code":"USD","value":"27.76"}}}
        """;
        var gateway = GatewayReturning(_ => StubHttpMessageHandler.Json(HttpStatusCode.Created, json), out _);

        var result = await gateway.CaptureAsync("order-1", "AUTH1", new Money(29m, "USD"), CancellationToken.None);

        Assert.Equal("CAP1", result.CaptureId);
        Assert.Equal(29.00m, result.CapturedAmount);
        Assert.Equal(1.24m, result.FeeAmount);
        Assert.Equal(27.76m, result.NetAmount);
    }

    [Fact]
    public async Task RefundAsync_returns_refund_result()
    {
        const string json = """{"id":"REF1","status":"COMPLETED","amount":{"currency_code":"USD","value":"5.00"}}""";
        var gateway = GatewayReturning(_ => StubHttpMessageHandler.Json(HttpStatusCode.Created, json), out _);

        var result = await gateway.RefundAsync("CAP1", new Money(5m, "USD"), "key-1", CancellationToken.None);

        Assert.Equal("REF1", result.RefundId);
        Assert.Equal("COMPLETED", result.Status);
        Assert.Equal(5.00m, result.Amount);
    }

    [Fact]
    public async Task VoidAsync_treats_204_no_content_as_success()
    {
        var gateway = GatewayReturning(_ => StubHttpMessageHandler.NoContent(), out _);

        // Should not throw even though the SDK declares a response body for void.
        await gateway.VoidAsync("order-1", "AUTH1", CancellationToken.None);
    }

    [Fact]
    public async Task VoidAsync_treats_409_as_idempotent_success()
    {
        const string json = """{"name":"RESOURCE_NOT_FOUND","message":"already voided","debug_id":"d2"}""";
        var gateway = GatewayReturning(_ => StubHttpMessageHandler.Json(HttpStatusCode.Conflict, json), out _);

        await gateway.VoidAsync("order-1", "AUTH1", CancellationToken.None);
    }
}
