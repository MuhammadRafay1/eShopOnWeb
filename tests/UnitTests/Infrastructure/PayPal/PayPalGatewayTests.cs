using System.Net;
using System.Net.Http;
using System.Threading;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.PayPal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PayPalServerSdk;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.Infrastructure.PayPal;

public class PayPalGatewayTests
{
    private const string OrderAuthorizedJson = """
        {"id":"ORD-1","status":"COMPLETED","purchase_units":[{"payments":{"authorizations":[
        {"id":"AUTH-1","status":"CREATED","amount":{"currency_code":"USD","value":"36.50"},"expiration_time":"2026-10-30T10:00:00Z"}]}}]}
        """;

    private const string CaptureJson = """
        {"id":"CAP-1","status":"COMPLETED","amount":{"currency_code":"USD","value":"36.50"},
        "seller_receivable_breakdown":{"gross_amount":{"currency_code":"USD","value":"36.50"},
        "paypal_fee":{"currency_code":"USD","value":"1.44"},"net_amount":{"currency_code":"USD","value":"35.06"}}}
        """;

    private const string RefundJson = """
        {"id":"REF-1","status":"COMPLETED","amount":{"currency_code":"USD","value":"10.00"}}
        """;

    private static PayPalGateway Gateway(StubHttpMessageHandler handler)
    {
        // Oauth2 left unset: no token round-trip, so the stub sees exactly the operation request(s).
        var client = new PayPalServerSdkClient(new HttpClient(handler), new PayPalServerSdkClientOptions());
        var options = Options.Create(new PayPalOptions { Currency = "USD", ClientId = "x", ClientSecret = "y", Environment = "sandbox" });
        return new PayPalGateway(client, options, NullLogger<PayPalGateway>.Instance);
    }

    private static AuthorizeCardPaymentCommand CardCommand(decimal amount = 36.50m) => new()
    {
        Amount = amount,
        InvoiceId = "ESHOP-TEST",
        IdempotencyKey = "pay-1-1",
        Card = new CardDetails
        {
            Number = "4111111111111111",
            Expiry = "2030-01",
            SecurityCode = "123",
            BillingAddress = new CardBillingAddress { CountryCode = "US" }
        }
    };

    [Fact]
    public async Task Authorize_success_reads_nested_authorization_and_sends_amount_to_the_cent()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, OrderAuthorizedJson);
        var gateway = Gateway(handler);

        var result = await gateway.AuthorizeAsync(CardCommand(), CancellationToken.None);

        Assert.Equal("ORD-1", result.PayPalOrderId);
        Assert.Equal("AUTH-1", result.AuthorizationId);
        Assert.Equal("CREATED", result.AuthorizationStatus);
        Assert.Equal(36.50m, result.AuthorizedAmount);
        Assert.Contains("\"value\":\"36.50\"", handler.Bodies[0]);        // amount sent to the cent
        Assert.Equal("pay-1-1", handler.HeaderOf(0, "PayPal-Request-Id")); // our real idempotency key
    }

    [Fact]
    public async Task Authorize_payer_action_required_is_a_stop_condition()
    {
        const string json = """{"id":"ORD-2","status":"PAYER_ACTION_REQUIRED","purchase_units":[]}""";
        var gateway = Gateway(StubHttpMessageHandler.Returning(HttpStatusCode.OK, json));

        await Assert.ThrowsAsync<PayPalPayerActionRequiredException>(
            () => gateway.AuthorizeAsync(CardCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task Authorize_typed_error_carries_status_and_debug_id()
    {
        const string json = """{"name":"UNPROCESSABLE_ENTITY","message":"refused","debug_id":"DBG-9","details":[{"issue":"TRANSACTION_REFUSED"}]}""";
        var gateway = Gateway(StubHttpMessageHandler.Returning((HttpStatusCode)422, json));

        var ex = await Assert.ThrowsAsync<PaymentGatewayException>(
            () => gateway.AuthorizeAsync(CardCommand(), CancellationToken.None));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, ex.StatusCode);
        Assert.Equal("DBG-9", ex.DebugId);
        Assert.IsNotType<PaymentOutcomeUnknownException>(ex);
    }

    [Fact]
    public async Task Authorize_drifted_body_surfaces_as_gateway_exception_not_escape()
    {
        // 2xx whose shape does not match Order -> ResponseDeserializationException inside the SDK.
        const string json = """{"id":"ORD-3","status":"COMPLETED","purchase_units":"not-an-array"}""";
        var gateway = Gateway(StubHttpMessageHandler.Returning(HttpStatusCode.OK, json));

        await Assert.ThrowsAsync<PaymentGatewayException>(
            () => gateway.AuthorizeAsync(CardCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task Capture_reads_seller_receivable_breakdown()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, CaptureJson);
        var gateway = Gateway(handler);

        var result = await gateway.CaptureAsync("AUTH-1", 36.50m, "fulfil-1", CancellationToken.None);

        Assert.Equal("CAP-1", result.CaptureId);
        Assert.Equal(36.50m, result.GrossAmount);
        Assert.Equal(1.44m, result.PayPalFee);
        Assert.Equal(35.06m, result.NetAmount);
        Assert.Contains("\"value\":\"36.50\"", handler.Bodies[0]);  // explicit capture amount
    }

    [Fact]
    public async Task Capture_connection_failure_retries_once_with_same_request_id_then_unknown()
    {
        var handler = StubHttpMessageHandler.Throwing();
        var gateway = Gateway(handler);

        await Assert.ThrowsAsync<PaymentOutcomeUnknownException>(
            () => gateway.CaptureAsync("AUTH-1", 36.50m, "fulfil-1", CancellationToken.None));

        Assert.Equal(2, handler.Requests.Count);                                  // one resend
        Assert.Equal("fulfil-1", handler.HeaderOf(0, "PayPal-Request-Id"));
        Assert.Equal("fulfil-1", handler.HeaderOf(1, "PayPal-Request-Id"));       // identical key
    }

    [Fact]
    public async Task Refund_success_returns_refund_id()
    {
        var gateway = Gateway(StubHttpMessageHandler.Returning(HttpStatusCode.OK, RefundJson));

        var result = await gateway.RefundAsync("CAP-1", 10.00m, "refund-1-r1", CancellationToken.None);

        Assert.Equal("REF-1", result.RefundId);
        Assert.Equal("COMPLETED", result.Status);
        Assert.Equal(10.00m, result.Amount);
    }

    [Fact]
    public async Task Get_authorization_is_a_read_and_connection_failure_is_not_unknown()
    {
        var gateway = Gateway(StubHttpMessageHandler.Throwing());

        var ex = await Assert.ThrowsAsync<PaymentGatewayException>(
            () => gateway.GetAuthorizationAsync("AUTH-1", CancellationToken.None));

        Assert.IsNotType<PaymentOutcomeUnknownException>(ex);   // a read is not an unknown write outcome
    }
}
