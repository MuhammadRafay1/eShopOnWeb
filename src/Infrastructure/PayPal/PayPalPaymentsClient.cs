using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Payments v2 client: capture / reauthorize / void an authorization and refund a capture. Built to
/// payments_payment_v2.json. Capture and refund send Prefer: return=representation so the response
/// carries the full breakdown (seller_receivable_breakdown / amount).
/// </summary>
public class PayPalPaymentsClient : PayPalClientBase, IPayPalPaymentsClient
{
    public PayPalPaymentsClient(HttpClient httpClient, IPayPalAccessTokenProvider tokenProvider)
        : base(httpClient, tokenProvider)
    {
    }

    public async Task<PayPalCaptureResult> CaptureAuthorizationAsync(
        int orderId, string authorizationId, decimal amount, string currencyCode, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["amount"] = Money(amount, currencyCode),
            ["final_capture"] = true
        };

        var root = await SendAsync(() =>
        {
            var req = new HttpRequestMessage(HttpMethod.Post, $"/v2/payments/authorizations/{authorizationId}/capture")
            {
                Content = JsonContent.Create(body)
            };
            req.Headers.Add("PayPal-Request-Id", $"order-capture-{PayPalRequestContext.RunToken}-{orderId}");
            req.Headers.Add("Prefer", "return=representation");
            return req;
        }, cancellationToken);

        return ParseCapture(root!.Value, currencyCode);
    }

    public async Task<PayPalAuthorizationResult> ReauthorizeAuthorizationAsync(
        int orderId, string authorizationId, decimal amount, string currencyCode, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["amount"] = Money(amount, currencyCode)
        };

        var root = await SendAsync(() =>
        {
            var req = new HttpRequestMessage(HttpMethod.Post, $"/v2/payments/authorizations/{authorizationId}/reauthorize")
            {
                Content = JsonContent.Create(body)
            };
            req.Headers.Add("PayPal-Request-Id", $"order-reauthorize-{PayPalRequestContext.RunToken}-{orderId}");
            req.Headers.Add("Prefer", "return=representation");
            return req;
        }, cancellationToken);

        return PayPalOrdersClient.ParseAuthorization(root!.Value);
    }

    public async Task VoidAuthorizationAsync(int orderId, string authorizationId, CancellationToken cancellationToken = default)
    {
        await SendAsync(() =>
        {
            var req = new HttpRequestMessage(HttpMethod.Post, $"/v2/payments/authorizations/{authorizationId}/void");
            req.Headers.Add("PayPal-Request-Id", $"order-void-{PayPalRequestContext.RunToken}-{orderId}");
            // Default Prefer: return=minimal -> 204 No Content.
            return req;
        }, cancellationToken);
    }

    public async Task<PayPalAuthorizationResult> GetAuthorizationAsync(string authorizationId, CancellationToken cancellationToken = default)
    {
        var root = await SendAsync(() =>
            new HttpRequestMessage(HttpMethod.Get, $"/v2/payments/authorizations/{authorizationId}"),
            cancellationToken);
        return PayPalOrdersClient.ParseAuthorization(root!.Value);
    }

    public async Task<PayPalRefundResult> RefundCaptureAsync(
        string captureId, decimal? amount, string currencyCode, string requestId, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>();
        if (amount.HasValue)
        {
            body["amount"] = Money(amount.Value, currencyCode);
        }

        var root = await SendAsync(() =>
        {
            var req = new HttpRequestMessage(HttpMethod.Post, $"/v2/payments/captures/{captureId}/refund")
            {
                Content = JsonContent.Create(body)
            };
            req.Headers.Add("PayPal-Request-Id", $"{PayPalRequestContext.RunToken}-{requestId}");
            req.Headers.Add("Prefer", "return=representation");
            return req;
        }, cancellationToken);

        return ParseRefund(root!.Value, currencyCode);
    }

    public async Task<PayPalCaptureResult> GetCaptureAsync(string captureId, CancellationToken cancellationToken = default)
    {
        var root = await SendAsync(() =>
            new HttpRequestMessage(HttpMethod.Get, $"/v2/payments/captures/{captureId}"),
            cancellationToken);
        return ParseCapture(root!.Value, "");
    }

    private static Dictionary<string, object?> Money(decimal amount, string currencyCode) => new()
    {
        ["currency_code"] = currencyCode,
        ["value"] = PayPalMoney.Format(amount, currencyCode)
    };

    private static PayPalCaptureResult ParseCapture(JsonElement root, string fallbackCurrency)
    {
        var result = new PayPalCaptureResult
        {
            Id = GetString(root, "id") ?? "",
            Status = GetString(root, "status") ?? "",
            CurrencyCode = fallbackCurrency
        };

        if (TryGetProperty(root, "amount", out var amount))
        {
            result.GrossAmount = GetMoneyValue(amount) ?? result.GrossAmount;
            result.CurrencyCode = GetString(amount, "currency_code") ?? result.CurrencyCode;
        }

        if (TryGetProperty(root, "seller_receivable_breakdown", out var breakdown))
        {
            if (TryGetProperty(breakdown, "gross_amount", out var gross))
            {
                result.GrossAmount = GetMoneyValue(gross) ?? result.GrossAmount;
                result.CurrencyCode = GetString(gross, "currency_code") ?? result.CurrencyCode;
            }
            if (TryGetProperty(breakdown, "paypal_fee", out var fee))
            {
                result.PayPalFeeAmount = GetMoneyValue(fee);
            }
            if (TryGetProperty(breakdown, "net_amount", out var net))
            {
                result.NetAmount = GetMoneyValue(net);
            }
        }

        return result;
    }

    private static PayPalRefundResult ParseRefund(JsonElement root, string fallbackCurrency)
    {
        var result = new PayPalRefundResult
        {
            Id = GetString(root, "id") ?? "",
            Status = GetString(root, "status") ?? "",
            CurrencyCode = fallbackCurrency
        };
        if (TryGetProperty(root, "amount", out var amount))
        {
            result.Amount = GetMoneyValue(amount) ?? 0m;
            result.CurrencyCode = GetString(amount, "currency_code") ?? result.CurrencyCode;
        }
        return result;
    }
}
