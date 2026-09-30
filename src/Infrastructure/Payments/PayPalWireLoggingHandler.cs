using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Payments;

// TEMPORARY diagnostic handler for verifying calls on the wire (dotnet-configuration-resilience
// guidance). Redacts card number/CVC before logging — never let raw card data reach a log line.
public class PayPalWireLoggingHandler : DelegatingHandler
{
    private readonly ILogger<PayPalWireLoggingHandler> _logger;

    private static readonly Regex NumberField = new("\"number\"\\s*:\\s*\"[^\"]*\"", RegexOptions.Compiled);
    private static readonly Regex SecurityCodeField = new("\"security_code\"\\s*:\\s*\"[^\"]*\"", RegexOptions.Compiled);

    public PayPalWireLoggingHandler(ILogger<PayPalWireLoggingHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var requestBody = request.Content is not null ? await request.Content.ReadAsStringAsync(ct) : null;
        _logger.LogWarning("PayPal --> {Method} {Uri} {Body}", request.Method, request.RequestUri, Redact(requestBody));

        var response = await base.SendAsync(request, ct);

        var responseBody = response.Content is not null ? await response.Content.ReadAsStringAsync(ct) : null;
        _logger.LogWarning("PayPal <-- {Status} {Body}", (int)response.StatusCode, Redact(responseBody));

        return response;
    }

    private static string? Redact(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return body;
        }

        body = NumberField.Replace(body, "\"number\":\"[REDACTED]\"");
        body = SecurityCodeField.Replace(body, "\"security_code\":\"[REDACTED]\"");
        return body;
    }
}
