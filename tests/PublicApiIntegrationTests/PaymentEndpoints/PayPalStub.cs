using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PublicApiIntegrationTests.PaymentEndpoints;

/// <summary>
/// Routes PayPal SDK HTTP calls to canned responses so the full endpoint → service → gateway path can be tested
/// without any live PayPal call. Covers the OAuth token endpoint and every operation the integration uses.
/// </summary>
public sealed class PayPalStubHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        var method = request.Method.Method;
        return Task.FromResult(Respond(path, method));
    }

    private static HttpResponseMessage Respond(string path, string method)
    {
        // OAuth2 token
        if (path.EndsWith("/v1/oauth2/token"))
            return Json(HttpStatusCode.OK, """{"access_token":"stub-token","token_type":"Bearer","expires_in":3600}""");

        // Create order (authorize) -> order completed with an authorization
        if (path.EndsWith("/v2/checkout/orders") && method == "POST")
            return Json(HttpStatusCode.Created, """
            {"id":"ORDER-STUB","status":"COMPLETED","purchase_units":[{"payments":{"authorizations":[
            {"id":"AUTH-STUB","status":"CREATED","expiration_time":"2099-01-01T00:00:00Z"}]}}]}
            """);

        // Capture
        if (path.Contains("/authorizations/") && path.EndsWith("/capture"))
            return Json(HttpStatusCode.Created, """
            {"id":"CAP-STUB","status":"COMPLETED","amount":{"currency_code":"USD","value":"29.00"},
            "seller_receivable_breakdown":{"gross_amount":{"currency_code":"USD","value":"29.00"},
            "paypal_fee":{"currency_code":"USD","value":"1.24"},"net_amount":{"currency_code":"USD","value":"27.76"}}}
            """);

        // Void
        if (path.Contains("/authorizations/") && path.EndsWith("/void"))
            return new HttpResponseMessage(HttpStatusCode.NoContent);

        // Refund
        if (path.Contains("/captures/") && path.EndsWith("/refund"))
            return Json(HttpStatusCode.Created, """{"id":"REF-STUB","status":"COMPLETED","amount":{"currency_code":"USD","value":"5.00"}}""");

        // Vault: setup token
        if (path.EndsWith("/v3/vault/setup-tokens"))
            return Json(HttpStatusCode.Created, """{"id":"SETUP-STUB","status":"APPROVED","payment_source":{"card":{"last_digits":"1111","brand":"VISA","expiry":"2030-01"}}}""");

        // Vault: payment token (exchange) + delete
        if (path.EndsWith("/v3/vault/payment-tokens") && method == "POST")
            return Json(HttpStatusCode.Created, """{"id":"PMTOKEN-STUB","payment_source":{"card":{"last_digits":"1111","brand":"VISA","expiry":"2030-01"}}}""");
        if (path.Contains("/v3/vault/payment-tokens/") && method == "DELETE")
            return new HttpResponseMessage(HttpStatusCode.NoContent);

        // Transaction search
        if (path.EndsWith("/v1/reporting/transactions"))
            return Json(HttpStatusCode.OK, """
            {"transaction_details":[{"transaction_info":{"transaction_id":"CAP-STUB",
            "transaction_amount":{"currency_code":"USD","value":"29.00"},"transaction_status":"S",
            "transaction_initiation_date":"2026-09-01T00:00:00Z"}}],"page":1,"total_pages":1,"total_items":1}
            """);

        return Json(HttpStatusCode.NotFound, """{"name":"RESOURCE_NOT_FOUND","message":"stub: no route","debug_id":"stub"}""");
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
}

/// <summary>A test host with the PayPal SDK HTTP client replaced by <see cref="PayPalStubHandler"/>.</summary>
public sealed class PayPalApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddHttpClient(Options.DefaultName)
                .ConfigurePrimaryHttpMessageHandler(() => new PayPalStubHandler());
        });
    }
}
