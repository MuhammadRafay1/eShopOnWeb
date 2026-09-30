using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

public class PayPalPaymentGateway : IPayPalPaymentGateway
{
    // Deterministic idempotency keys are derived from the app's own integer ids so a genuine
    // retry of the same logical request reuses the same PayPal-Request-Id. Those integer ids
    // come from an EF store that (in this sandbox's in-memory mode) restarts numbering from 1
    // on every process restart, while PayPal remembers request ids for hours-to-weeks. Salting
    // the key with a value fixed for the lifetime of this process keeps retries within a single
    // run idempotent while guaranteeing a fresh process (a fresh id sequence) never collides
    // with a previous run's PayPal-side idempotency record for the "same" order id.
    private static readonly string RunSalt = Guid.NewGuid().ToString("N")[..8];

    private readonly PayPalClient _client;
    private readonly PayPalOptions _options;

    public PayPalPaymentGateway(PayPalClient client, IOptions<PayPalOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public string Currency => _options.Currency;

    public async Task<PayPalAuthorizationResult> AuthorizeAsync(PayPalAuthorizeRequest request, CancellationToken cancellationToken = default)
    {
        var createBody = new PpCreateOrderRequest
        {
            Intent = "AUTHORIZE",
            PurchaseUnits = new List<PpPurchaseUnitRequest>
            {
                new()
                {
                    ReferenceId = "default",
                    CustomId = request.OrderId.ToString(CultureInfo.InvariantCulture),
                    InvoiceId = request.InvoiceId,
                    Amount = new PpMoney { CurrencyCode = Currency, Value = FormatAmount(request.Amount) }
                }
            },
            PaymentSource = new PpPaymentSourceRequest { Card = BuildCardRequest(request.Card, request.VaultId) }
        };

        var createResponse = await _client.SendAsync<PpOrderResponse>(
            HttpMethod.Post, "/v2/checkout/orders", createBody, $"pay-order-{request.OrderId}-{RunSalt}", cancellationToken);
        EnsureNoActionRequired(createResponse);

        // With a valid payment_source supplied up front (card-direct), PayPal authorizes the
        // order as part of creating it (single-step) and the authorization is already present
        // in the create-order response. Only fall back to the separate /authorize call for the
        // two-step case (no payment_source yet / still APPROVED, not COMPLETED).
        var orderResponse = createResponse;
        var authorization = orderResponse.PurchaseUnits?.FirstOrDefault()?.Payments?.Authorizations?.FirstOrDefault();
        if (authorization is null)
        {
            orderResponse = await _client.SendAsync<PpOrderResponse>(
                HttpMethod.Post, $"/v2/checkout/orders/{createResponse.Id}/authorize", null, $"authorize-{request.OrderId}-{RunSalt}", cancellationToken);
            EnsureNoActionRequired(orderResponse);
            authorization = orderResponse.PurchaseUnits?.FirstOrDefault()?.Payments?.Authorizations?.FirstOrDefault()
                ?? throw new PayPalGatewayException("PayPal did not return an authorization for the order.");
        }

        var amount = ParseAmount(authorization.Amount);
        if (Math.Abs(amount - request.Amount) > 0.005m)
            throw new PayPalGatewayException($"PayPal authorized {amount} {Currency} but the order total is {request.Amount} {Currency}.");

        return new PayPalAuthorizationResult(createResponse.Id, authorization.Id, authorization.Status, ParseDate(authorization.ExpirationTime), amount);
    }

    public async Task<PayPalAuthorizationResult> ReauthorizeAsync(int orderId, string authorizationId, decimal amount, CancellationToken cancellationToken = default)
    {
        var body = new PpAmountOnlyRequest { Amount = new PpMoney { CurrencyCode = Currency, Value = FormatAmount(amount) } };
        var response = await _client.SendAsync<PpAuthorization>(
            HttpMethod.Post, $"/v2/payments/authorizations/{authorizationId}/reauthorize", body, $"reauthorize-{authorizationId}", cancellationToken);

        return new PayPalAuthorizationResult(string.Empty, response.Id, response.Status, ParseDate(response.ExpirationTime), ParseAmount(response.Amount));
    }

    public async Task<PayPalCaptureResult> CaptureAsync(int orderId, string authorizationId, CancellationToken cancellationToken = default)
    {
        var body = new PpCaptureRequest { FinalCapture = true };
        var capture = await _client.SendAsync<PpCapture>(
            HttpMethod.Post, $"/v2/payments/authorizations/{authorizationId}/capture", body, $"capture-{orderId}-{RunSalt}", cancellationToken);

        var captured = ParseAmount(capture.Amount);
        var fee = capture.SellerReceivableBreakdown?.PaypalFee is { } feeMoney ? ParseAmount(feeMoney) : (decimal?)null;
        var net = capture.SellerReceivableBreakdown?.NetAmount is { } netMoney ? ParseAmount(netMoney) : (decimal?)null;

        return new PayPalCaptureResult(capture.Id, capture.Status, captured, fee, net);
    }

    public Task VoidAsync(int orderId, string authorizationId, CancellationToken cancellationToken = default) =>
        _client.SendAsync(HttpMethod.Post, $"/v2/payments/authorizations/{authorizationId}/void", null, $"void-{orderId}-{RunSalt}", cancellationToken);

    public async Task<PayPalRefundResult> RefundAsync(PayPalRefundRequest request, CancellationToken cancellationToken = default)
    {
        var body = new PpRefundRequest
        {
            Amount = request.Amount.HasValue ? new PpMoney { CurrencyCode = Currency, Value = FormatAmount(request.Amount.Value) } : null,
            CustomId = request.OrderId,
            InvoiceId = request.InvoiceId
        };

        var refund = await _client.SendAsync<PpRefundResponse>(
            HttpMethod.Post, $"/v2/payments/captures/{request.CaptureId}/refund", body, request.IdempotencyKey, cancellationToken);

        var amount = ParseAmount(refund.Amount);
        var total = refund.SellerPayableBreakdown?.TotalRefundedAmount is { } totalMoney ? ParseAmount(totalMoney) : amount;

        return new PayPalRefundResult(refund.Id, refund.Status, amount, total);
    }

    public async Task<PayPalVaultResult> SaveCardAsync(CardDetails card, string? existingCustomerId, CancellationToken cancellationToken = default)
    {
        var body = new PpVaultCreateRequest
        {
            PaymentSource = new PpVaultPaymentSourceRequest { Card = BuildCardRequest(card, vaultId: null) },
            Customer = existingCustomerId is null ? null : new PpCustomerRequest { Id = existingCustomerId }
        };

        var response = await _client.SendAsync<PpVaultTokenResponse>(
            HttpMethod.Post, "/v3/vault/payment-tokens", body, $"vault-{Guid.NewGuid():N}", cancellationToken);

        var approvalLink = response.Links?.FirstOrDefault(l => l.Rel is "approve" or "payer-action" or "confirm");
        if (approvalLink is not null)
            throw new PayPalActionRequiredException(
                $"PayPal returned a '{approvalLink.Rel}' link when vaulting this card, meaning the payer must approve in a browser. " +
                "This integration is card-direct only and does not implement an approval round-trip.");

        var cardResponse = response.PaymentSource?.Card;
        return new PayPalVaultResult(response.Id, response.Customer?.Id, cardResponse?.LastDigits, cardResponse?.Brand, cardResponse?.Expiry);
    }

    public async Task DeleteVaultedCardAsync(string vaultTokenId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.SendAsync(HttpMethod.Delete, $"/v3/vault/payment-tokens/{vaultTokenId}", null, null, cancellationToken);
        }
        catch (PayPalGatewayException ex) when (ex.StatusCode == 404)
        {
            // Already gone at PayPal - deleting locally is still a success.
        }
    }

    public async Task<IReadOnlyList<PayPalTransactionRecord>> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var results = new List<PayPalTransactionRecord>();
        var seen = new HashSet<string>();
        var windowStart = from;

        while (windowStart < to)
        {
            var windowEnd = windowStart.AddDays(31) < to ? windowStart.AddDays(31) : to;

            var page = 1;
            var totalPages = 1;
            do
            {
                var path = $"/v1/reporting/transactions?start_date={Uri.EscapeDataString(FormatDate(windowStart))}" +
                           $"&end_date={Uri.EscapeDataString(FormatDate(windowEnd))}&fields=transaction_info&page_size=100&page={page}";
                var response = await _client.SendAsync<PpTransactionSearchResponse>(HttpMethod.Get, path, null, null, cancellationToken);
                totalPages = Math.Max(response.TotalPages, 1);

                foreach (var detail in response.TransactionDetails ?? new List<PpTransactionDetail>())
                {
                    var info = detail.TransactionInfo;
                    if (info is null)
                        continue;

                    var key = $"{info.TransactionId}|{info.TransactionInitiationDate}";
                    if (!seen.Add(key))
                        continue;

                    results.Add(new PayPalTransactionRecord(
                        info.TransactionId ?? string.Empty,
                        ParseDate(info.TransactionInitiationDate) ?? windowStart,
                        info.TransactionAmount is null ? 0m : ParseAmount(info.TransactionAmount),
                        info.FeeAmount is null ? null : ParseAmount(info.FeeAmount),
                        info.TransactionStatus ?? string.Empty,
                        info.CustomField,
                        info.InvoiceId));
                }

                page++;
            } while (page <= totalPages);

            windowStart = windowEnd;
        }

        return results;
    }

    private static PpCardRequest BuildCardRequest(CardDetails? card, string? vaultId)
    {
        if (!string.IsNullOrEmpty(vaultId))
            return new PpCardRequest { VaultId = vaultId };

        if (card is null)
            throw new ArgumentException("Either card details or a saved payment method id must be supplied.");

        return new PpCardRequest
        {
            Name = card.Name,
            Number = card.Number,
            Expiry = card.ToPayPalExpiry(),
            SecurityCode = card.SecurityCode,
            BillingAddress = new PpAddress
            {
                CountryCode = card.BillingAddress.CountryCode,
                AddressLine1 = card.BillingAddress.AddressLine1,
                AdminArea1 = card.BillingAddress.AdminArea1,
                AdminArea2 = card.BillingAddress.AdminArea2,
                PostalCode = card.BillingAddress.PostalCode
            },
            Attributes = new PpCardAttributes { Verification = new PpCardVerification { Method = "SCA_WHEN_REQUIRED" } }
        };
    }

    private static void EnsureNoActionRequired(PpOrderResponse order)
    {
        if (string.Equals(order.Status, "PAYER_ACTION_REQUIRED", StringComparison.OrdinalIgnoreCase))
            throw new PayPalActionRequiredException(
                "PayPal reported order status PAYER_ACTION_REQUIRED - the payer must complete a browser/3DS approval step. " +
                "This integration is card-direct only and does not implement an approval round-trip.");

        var actionLink = order.Links?.FirstOrDefault(l => l.Rel is "payer-action" or "approve");
        if (actionLink is not null)
            throw new PayPalActionRequiredException(
                $"PayPal returned a '{actionLink.Rel}' approval link - the payer must act in a browser before this payment can proceed. " +
                "This integration is card-direct only and does not implement an approval round-trip.");
    }

    private static string FormatAmount(decimal amount) => amount.ToString("F2", CultureInfo.InvariantCulture);

    private static string FormatDate(DateTimeOffset date) => date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private static decimal ParseAmount(PpMoney? money) =>
        money is null ? 0m : decimal.Parse(money.Value, NumberStyles.Number, CultureInfo.InvariantCulture);

    private static DateTimeOffset? ParseDate(string? value) =>
        string.IsNullOrEmpty(value) ? null : DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
