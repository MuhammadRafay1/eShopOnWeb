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
/// Payment Method Tokens v3 client: vault (save) a card and delete a saved card. Built to
/// vault_payment_tokens_v3.json. The card is vaulted directly (no setup-token round trip is needed
/// for a direct card). The shopper is identified only by customer.merchant_customer_id — the
/// PayPal-generated customer.id (returned on the response) is captured too, when present.
/// </summary>
public class PayPalVaultClient : PayPalClientBase, IPayPalVaultClient
{
    public PayPalVaultClient(HttpClient httpClient, IPayPalAccessTokenProvider tokenProvider)
        : base(httpClient, tokenProvider)
    {
    }

    public async Task<PayPalPaymentTokenResult> CreatePaymentTokenAsync(
        string merchantCustomerId, CardDetails card, string requestId, CancellationToken cancellationToken = default)
    {
        // Two-step vaulting per the spec: create a setup token from the raw card, then exchange it
        // for a durable payment token. For a direct card the setup token is APPROVED immediately
        // (no browser step). This is used instead of posting the raw card straight to
        // /v3/vault/payment-tokens because that path is not supported on this account.
        var setupTokenId = await CreateSetupTokenAsync(merchantCustomerId, card, requestId, cancellationToken);

        var body = new Dictionary<string, object?>
        {
            ["customer"] = new Dictionary<string, object?>
            {
                ["merchant_customer_id"] = merchantCustomerId
            },
            ["payment_source"] = new Dictionary<string, object?>
            {
                ["token"] = new Dictionary<string, object?>
                {
                    ["id"] = setupTokenId,
                    ["type"] = "SETUP_TOKEN"
                }
            }
        };

        var root = await SendAsync(() =>
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/v3/vault/payment-tokens")
            {
                Content = JsonContent.Create(body)
            };
            req.Headers.Add("PayPal-Request-Id", $"{requestId}-pt");
            return req;
        }, cancellationToken);

        return ParseToken(root!.Value);
    }

    private async Task<string> CreateSetupTokenAsync(
        string merchantCustomerId, CardDetails card, string requestId, CancellationToken cancellationToken)
    {
        var cardNode = new Dictionary<string, object?>();
        PayPalCardSerializer.PopulateRawCard(cardNode, card);

        // NOTE: the shopper (customer.merchant_customer_id) is associated at the payment-token step,
        // not here — including a customer on the setup-token request is rejected by this account.
        var body = new Dictionary<string, object?>
        {
            ["payment_source"] = new Dictionary<string, object?>
            {
                ["card"] = cardNode
            }
        };

        var root = await SendAsync(() =>
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/v3/vault/setup-tokens")
            {
                Content = JsonContent.Create(body)
            };
            req.Headers.Add("PayPal-Request-Id", $"{requestId}-st");
            return req;
        }, cancellationToken);

        var status = GetString(root!.Value, "status");
        var setupTokenId = GetString(root.Value, "id");
        if (string.IsNullOrEmpty(setupTokenId))
        {
            throw new PayPalApiException(502, "NO_SETUP_TOKEN",
                "PayPal did not return a setup token id when saving the card.", null, null);
        }

        // A direct card should be APPROVED without a browser step. Anything else would require payer
        // action, which this integration does not implement.
        if (!string.IsNullOrEmpty(status) && status != "APPROVED")
        {
            throw new PayPalPayerActionRequiredException(setupTokenId!);
        }

        return setupTokenId!;
    }

    public async Task DeletePaymentTokenAsync(string vaultId, CancellationToken cancellationToken = default)
    {
        try
        {
            await SendAsync(() =>
                new HttpRequestMessage(HttpMethod.Delete, $"/v3/vault/payment-tokens/{vaultId}"),
                cancellationToken);
        }
        catch (PayPalApiException ex) when (ex.HttpStatusCode == 404)
        {
            // Already gone — the end state ("not usable to pay") already holds, so treat as success.
        }
    }

    private static PayPalPaymentTokenResult ParseToken(JsonElement root)
    {
        var result = new PayPalPaymentTokenResult
        {
            Id = GetString(root, "id") ?? ""
        };

        if (TryGetProperty(root, "customer", out var customer))
        {
            result.CustomerId = GetString(customer, "id");
        }

        if (TryGetProperty(root, "payment_source", out var source)
            && TryGetProperty(source, "card", out var card))
        {
            result.Brand = GetString(card, "brand");
            result.LastDigits = GetString(card, "last_digits");
            result.Expiry = GetString(card, "expiry");
        }

        return result;
    }
}
