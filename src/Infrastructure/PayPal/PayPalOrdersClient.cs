using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Orders v2 client: creates a PayPal order with intent=AUTHORIZE and a direct-card (or vaulted)
/// payment source, then authorizes it to place the hold. Built to checkout_orders_v2.json.
/// </summary>
public class PayPalOrdersClient : PayPalClientBase, IPayPalOrdersClient
{
    private readonly HttpClient _httpClient;

    public PayPalOrdersClient(HttpClient httpClient, IPayPalAccessTokenProvider tokenProvider)
        : base(httpClient, tokenProvider)
    {
        _httpClient = httpClient;
    }

    public async Task<PayPalOrderResult> CreateOrderAsync(CreateOrderInput input, CancellationToken cancellationToken = default)
    {
        var card = new Dictionary<string, object?>();
        if (!string.IsNullOrEmpty(input.VaultId))
        {
            card["vault_id"] = input.VaultId;
        }
        else
        {
            PayPalCardSerializer.PopulateRawCard(card, input.Card!);
        }

        var body = new Dictionary<string, object?>
        {
            ["intent"] = "AUTHORIZE",
            ["purchase_units"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["reference_id"] = "default",
                    ["invoice_id"] = InvoiceId(input.OrderId),
                    ["custom_id"] = input.OrderId.ToString(),
                    ["amount"] = new Dictionary<string, object?>
                    {
                        ["currency_code"] = input.CurrencyCode,
                        ["value"] = PayPalMoney.Format(input.Amount, input.CurrencyCode)
                    }
                }
            },
            ["payment_source"] = new Dictionary<string, object?>
            {
                ["card"] = card
            }
        };

        var root = await SendAsync(() =>
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/v2/checkout/orders")
            {
                Content = JsonContent.Create(body)
            };
            req.Headers.Add("PayPal-Request-Id", $"order-create-{PayPalRequestContext.RunToken}-{input.OrderId}");
            req.Headers.Add("Prefer", "return=representation");
            return req;
        }, cancellationToken);

        var result = ParseOrder(root!.Value);
        result.InvoiceId = InvoiceId(input.OrderId);
        return result;
    }

    public async Task<PayPalAuthorizationResult> AuthorizeOrderAsync(int orderId, string payPalOrderId, CancellationToken cancellationToken = default)
    {
        var root = await SendAsync(() =>
        {
            var req = new HttpRequestMessage(HttpMethod.Post, $"/v2/checkout/orders/{payPalOrderId}/authorize")
            {
                // The card was supplied at create time, so an empty body authorizes it.
                Content = JsonContent.Create(new Dictionary<string, object?>())
            };
            req.Headers.Add("PayPal-Request-Id", $"order-authorize-{PayPalRequestContext.RunToken}-{orderId}");
            req.Headers.Add("Prefer", "return=representation");
            return req;
        }, cancellationToken);

        var order = ParseOrder(root!.Value);
        if (order.PayerActionRequired)
        {
            // Surface as a payer-action order so the caller can stop-and-report per the task rules.
            return order.Authorization ?? new PayPalAuthorizationResult { Status = "PENDING" };
        }
        return order.Authorization
            ?? throw new ApplicationCore.Exceptions.PayPalApiException(502, "NO_AUTHORIZATION",
                "PayPal did not return an authorization for the order.", null, null);
    }

    public async Task<PayPalOrderResult> GetOrderAsync(string payPalOrderId, CancellationToken cancellationToken = default)
    {
        var root = await SendAsync(() =>
            new HttpRequestMessage(HttpMethod.Get, $"/v2/checkout/orders/{payPalOrderId}"),
            cancellationToken);
        return ParseOrder(root!.Value);
    }

    // invoice_id carries the order id (for reconciliation) plus the run token (for global
    // uniqueness the sandbox account requires). Reconciliation parses the order id back out.
    internal static string InvoiceId(int orderId) => $"eshop-order-{orderId}-{PayPalRequestContext.RunToken}";

    private static PayPalOrderResult ParseOrder(JsonElement root)
    {
        var status = GetString(root, "status") ?? "";
        var result = new PayPalOrderResult
        {
            Id = GetString(root, "id") ?? "",
            Status = status,
            PayerActionRequired = status == "PAYER_ACTION_REQUIRED"
        };

        // Dig into purchase_units[0].payments.authorizations[0] if present.
        if (TryGetProperty(root, "purchase_units", out var pus) && pus.ValueKind == JsonValueKind.Array)
        {
            foreach (var pu in pus.EnumerateArray())
            {
                if (TryGetProperty(pu, "payments", out var payments)
                    && TryGetProperty(payments, "authorizations", out var auths)
                    && auths.ValueKind == JsonValueKind.Array)
                {
                    foreach (var auth in auths.EnumerateArray())
                    {
                        result.Authorization = ParseAuthorization(auth);
                        break;
                    }
                }
                if (result.Authorization is not null) break;
            }
        }

        return result;
    }

    internal static PayPalAuthorizationResult ParseAuthorization(JsonElement auth)
    {
        var result = new PayPalAuthorizationResult
        {
            Id = GetString(auth, "id") ?? "",
            Status = GetString(auth, "status") ?? "",
            ExpirationTime = GetDateTime(auth, "expiration_time")
        };
        if (TryGetProperty(auth, "amount", out var amount))
        {
            result.Amount = GetMoneyValue(amount) ?? 0m;
            result.CurrencyCode = GetString(amount, "currency_code") ?? "";
        }
        return result;
    }
}
