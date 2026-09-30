using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Services;
using NSubstitute;
using PayPalServerSdk;
using PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials;
using PayPalServerSdk.Servers;
using Xunit;

namespace Microsoft.eShopWeb.IntegrationTests.PaymentGateway;

/// <summary>
/// Tests the SDK-response → plain-record translation and the SDK-error → domain-exception mapping in
/// isolation, by faking the SDK's HttpClient seam. No real network calls — real PayPal connectivity is
/// covered by the manual sandbox verification.
/// </summary>
public class PayPalPaymentGatewayServiceTests
{
    private const string TokenJson = """{"access_token":"test-token","token_type":"Bearer","expires_in":3600}""";

    private sealed class RoutingHandler : HttpMessageHandler
    {
        private readonly Func<string, (HttpStatusCode, string)> _route;
        public RoutingHandler(Func<string, (HttpStatusCode, string)> route) => _route = route;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.AbsoluteUri;
            var (status, body) = url.Contains("/v1/oauth2/token")
                ? (HttpStatusCode.OK, TokenJson)
                : _route(url);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private static PayPalPaymentGatewayService ServiceThatRoutes(Func<string, (HttpStatusCode, string)> route)
    {
        var client = new PayPalServerSdkClient(new HttpClient(new RoutingHandler(route)),
            new PayPalServerSdkClientOptions
            {
                Environment = ServerEnvironment.Sandbox,
                Oauth2 = new OAuth2ClientCredentials { ClientId = "id", ClientSecret = "secret" }
            });
        return new PayPalPaymentGatewayService(client, Substitute.For<IAppLogger<PayPalPaymentGatewayService>>());
    }

    [Fact]
    public async Task AuthorizeAsync_TranslatesAuthorizationOutOfNestedEnvelope()
    {
        var service = ServiceThatRoutes(url =>
        {
            if (url.Contains("/authorize"))
                return (HttpStatusCode.OK, """
                {"id":"ORDER-1","status":"COMPLETED","purchase_units":[
                  {"payments":{"authorizations":[
                    {"id":"AUTH-1","status":"CREATED","expiration_time":"2027-01-01T00:00:00Z"}]}}]}
                """);
            return (HttpStatusCode.OK, """{"id":"ORDER-1","status":"CREATED"}""");
        });

        var card = new CardDetails("Buyer", "4111111111111111", "2027-12", "123", null);
        var result = await service.AuthorizeAsync(new AuthorizationRequest(39m, "USD", card, null, "order:1"));

        Assert.False(result.PayerActionRequired);
        Assert.Equal("ORDER-1", result.PayPalOrderId);
        Assert.Equal("AUTH-1", result.AuthorizationId);
        Assert.Equal("CREATED", result.Status);   // raw wire value, not the StringEnum ToString()
        Assert.NotNull(result.ExpiresAt);
    }

    [Fact]
    public async Task AuthorizeAsync_DetectsPayerActionRequired()
    {
        var service = ServiceThatRoutes(url =>
            url.Contains("/authorize")
                ? (HttpStatusCode.OK, """{"id":"ORDER-1","status":"PAYER_ACTION_REQUIRED"}""")
                : (HttpStatusCode.OK, """{"id":"ORDER-1","status":"CREATED"}"""));

        var card = new CardDetails("Buyer", "4111111111111111", "2027-12", "123", null);
        var result = await service.AuthorizeAsync(new AuthorizationRequest(39m, "USD", card, null, "order:1"));

        Assert.True(result.PayerActionRequired);
        Assert.Null(result.AuthorizationId);
    }

    [Fact]
    public async Task CaptureAsync_ReportsFeeAndNetFromSellerReceivableBreakdown()
    {
        var service = ServiceThatRoutes(_ => (HttpStatusCode.OK, """
            {"id":"CAP-1","status":"COMPLETED",
             "amount":{"currency_code":"USD","value":"39.00"},
             "seller_receivable_breakdown":{
                "gross_amount":{"currency_code":"USD","value":"39.00"},
                "paypal_fee":{"currency_code":"USD","value":"1.50"},
                "net_amount":{"currency_code":"USD","value":"37.50"}}}
            """));

        var result = await service.CaptureAsync("AUTH-1", 39m, "USD", "1");

        Assert.Equal("CAP-1", result.CaptureId);
        Assert.Equal("COMPLETED", result.Status);
        Assert.Equal(39.00m, result.Amount);
        Assert.Equal(1.50m, result.PayPalFee);
        Assert.Equal(37.50m, result.NetAmount);
    }

    [Fact]
    public async Task AuthorizeAsync_TranslatesApiRejectionIntoPaymentGatewayException()
    {
        var service = ServiceThatRoutes(_ => (HttpStatusCode.UnprocessableEntity, """
            {"name":"UNPROCESSABLE_ENTITY","message":"Business validation failed.","debug_id":"abc123",
             "details":[{"issue":"INSTRUMENT_DECLINED","description":"The card was declined."}]}
            """));

        var card = new CardDetails("Buyer", "4111111111111111", "2027-12", "123", null);
        var ex = await Assert.ThrowsAsync<PaymentGatewayException>(
            () => service.AuthorizeAsync(new AuthorizationRequest(39m, "USD", card, null, "order:1")));

        Assert.Equal("UNPROCESSABLE_ENTITY", ex.PayPalErrorName);
        Assert.Equal("abc123", ex.PayPalDebugId);
    }

    [Fact]
    public async Task VaultCardAsync_ReturnsDisplaySafeSummaryOnly()
    {
        var service = ServiceThatRoutes(_ => (HttpStatusCode.OK, """
            {"id":"VAULT-1","payment_source":{"card":{"last_digits":"1111","brand":"VISA","expiry":"2027-12"}}}
            """));

        var result = await service.VaultCardAsync(new CardDetails("Buyer", "4111111111111111", "2027-12", "123", null));

        Assert.Equal("VAULT-1", result.VaultId);
        Assert.Equal("VISA", result.Brand);
        Assert.Equal("1111", result.LastDigits);
        Assert.Equal("2027-12", result.Expiry);
    }
}
