using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.PayPal.Models;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Implements the ApplicationCore-facing <see cref="IPayPalGateway"/> against the spec-shaped
/// <see cref="PayPalApiClient"/>. Owns all mapping between PayPal's wire shapes and the plain
/// domain results ApplicationCore consumes, including the create-order -> (fallback) authorize
/// sequence, SCA/challenge detection, and Transaction Search pagination/chunking.
/// </summary>
public class PayPalGateway : IPayPalGateway
{
    private readonly PayPalApiClient _client;

    public PayPalGateway(PayPalApiClient client)
    {
        _client = client;
    }

    public async Task<AuthorizeResult> AuthorizeAsync(decimal amount, string currency, CardDetails? card, string? vaultId, string invoiceId, string requestId, CancellationToken ct = default)
    {
        if ((card is null) == (vaultId is null))
        {
            throw new ArgumentException("Exactly one of card or vaultId must be supplied.");
        }

        var orderRequest = new OrderRequest
        {
            Intent = "AUTHORIZE",
            PurchaseUnits = new List<PurchaseUnitRequest>
            {
                new()
                {
                    InvoiceId = invoiceId,
                    CustomId = invoiceId,
                    Amount = new AmountRequest { CurrencyCode = currency, Value = FormatAmount(amount, currency) }
                }
            },
            PaymentSource = new PaymentSourceRequest
            {
                Card = card is not null
                    ? new CardRequest
                    {
                        Name = card.Name,
                        Number = card.Number,
                        Expiry = card.Expiry,
                        SecurityCode = card.SecurityCode,
                        BillingAddress = ToBillingAddress(card)
                    }
                    : new CardRequest { VaultId = vaultId }
            }
        };

        var order = await _client.CreateOrderAsync(orderRequest, requestId, ct);
        EnsureNoChallenge(order);

        var authorization = order.PurchaseUnits?.FirstOrDefault()?.Payments?.Authorizations?.FirstOrDefault();
        if (authorization is null)
        {
            // Defensive fallback per the spec: some flows return an APPROVED order without an
            // inline authorization, requiring an explicit /authorize call.
            order = await _client.AuthorizeOrderAsync(order.Id, requestId + "-auth", ct);
            EnsureNoChallenge(order);
            authorization = order.PurchaseUnits?.FirstOrDefault()?.Payments?.Authorizations?.FirstOrDefault();
        }

        if (authorization is null)
        {
            throw new PayPalGatewayException(System.Net.HttpStatusCode.UnprocessableEntity, "missing_authorization",
                "PayPal did not return an authorization for this order.", null);
        }

        var responseCard = order.PaymentSource?.Card;
        return new AuthorizeResult(order.Id, authorization.Id, authorization.Status, authorization.ExpirationTime, responseCard?.Brand, responseCard?.LastDigits);
    }

    public async Task<CaptureResult> CaptureAsync(string authorizationId, decimal amount, string currency, string invoiceId, string requestId, CancellationToken ct = default)
    {
        var request = new CaptureRequest
        {
            Amount = new Money { CurrencyCode = currency, Value = FormatAmount(amount, currency) },
            FinalCapture = true,
            InvoiceId = invoiceId
        };

        var capture = await _client.CaptureAuthorizationAsync(authorizationId, request, requestId, ct);
        var breakdown = capture.SellerReceivableBreakdown
            ?? throw new PayPalGatewayException(System.Net.HttpStatusCode.UnprocessableEntity, "missing_breakdown",
                "PayPal capture response did not include a seller_receivable_breakdown.", null);

        return new CaptureResult(
            capture.Id,
            capture.Status,
            ParseAmount(breakdown.GrossAmount.Value),
            breakdown.PayPalFee is not null ? ParseAmount(breakdown.PayPalFee.Value) : null,
            breakdown.NetAmount is not null ? ParseAmount(breakdown.NetAmount.Value) : null);
    }

    public async Task<AuthorizeResult> ReauthorizeAsync(string authorizationId, decimal amount, string currency, string requestId, CancellationToken ct = default)
    {
        var request = new ReauthorizeRequest
        {
            Amount = new Money { CurrencyCode = currency, Value = FormatAmount(amount, currency) }
        };

        var authorization = await _client.ReauthorizeAsync(authorizationId, request, requestId, ct);
        return new AuthorizeResult(string.Empty, authorization.Id, authorization.Status, authorization.ExpirationTime, null, null);
    }

    public Task VoidAsync(string authorizationId, string requestId, CancellationToken ct = default)
        => _client.VoidAuthorizationAsync(authorizationId, requestId, ct);

    public async Task<AuthorizationInfo> GetAuthorizationAsync(string authorizationId, CancellationToken ct = default)
    {
        var authorization = await _client.GetAuthorizationAsync(authorizationId, ct);
        return new AuthorizationInfo(authorization.Status, authorization.ExpirationTime);
    }

    public async Task<RefundResult> RefundAsync(string captureId, decimal? amount, string currency, string idempotencyKey, CancellationToken ct = default)
    {
        var request = new RefundRequest
        {
            Amount = amount.HasValue ? new Money { CurrencyCode = currency, Value = FormatAmount(amount.Value, currency) } : null
        };

        var refund = await _client.RefundCaptureAsync(captureId, request, idempotencyKey, ct);
        decimal? totalRefunded = refund.SellerPayableBreakdown?.TotalRefundedAmount is not null
            ? ParseAmount(refund.SellerPayableBreakdown.TotalRefundedAmount.Value)
            : null;

        return new RefundResult(refund.Id, refund.Status, totalRefunded);
    }

    public async Task<SavedCardInfo> SaveCardAsync(CardDetails card, string? customerId, string requestId, CancellationToken ct = default)
    {
        var request = new PaymentTokenRequest
        {
            Customer = customerId is not null ? new VaultCustomer { Id = customerId } : null,
            PaymentSource = new VaultPaymentSourceRequest
            {
                Card = new VaultCardRequest
                {
                    Name = card.Name,
                    Number = card.Number,
                    Expiry = card.Expiry,
                    SecurityCode = card.SecurityCode,
                    BillingAddress = ToBillingAddress(card)
                }
            }
        };

        var token = await _client.CreatePaymentTokenAsync(request, requestId, ct);
        var responseCard = token.PaymentSource?.Card;
        return new SavedCardInfo(token.Id, token.Customer?.Id, responseCard?.Brand, responseCard?.LastDigits, responseCard?.Expiry, responseCard?.Type);
    }

    public Task DeleteCardAsync(string vaultId, CancellationToken ct = default)
        => _client.DeletePaymentTokenAsync(vaultId, ct);

    public async Task<IReadOnlyList<PayPalTransaction>> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var results = new List<PayPalTransaction>();
        if (to <= from)
        {
            return results;
        }

        var windowStart = from;
        while (windowStart < to)
        {
            // Stay safely under the spec's 31-day-per-call maximum.
            var windowEnd = windowStart.AddDays(30) < to ? windowStart.AddDays(30) : to;

            var page = 1;
            int totalPages;
            do
            {
                var response = await _client.SearchTransactionsAsync(ToRfc3339(windowStart), ToRfc3339(windowEnd), page, 500, ct);
                totalPages = Math.Max(response.TotalPages, 1);

                foreach (var detail in response.TransactionDetails ?? new List<TransactionDetail>())
                {
                    var info = detail.TransactionInfo;
                    if (info is null) continue;

                    results.Add(new PayPalTransaction(
                        info.TransactionId ?? string.Empty,
                        info.TransactionStatus ?? string.Empty,
                        info.TransactionAmount is not null ? ParseAmount(info.TransactionAmount.Value) : 0m,
                        info.FeeAmount is not null ? ParseAmount(info.FeeAmount.Value) : null,
                        info.InvoiceId,
                        info.TransactionInitiationDate));
                }

                page++;
            } while (page <= totalPages);

            windowStart = windowEnd;
        }

        return results;
    }

    private static void EnsureNoChallenge(OrderResponse order)
    {
        var hasPayerActionLink = order.Links?.Any(l => string.Equals(l.Rel, "payer-action", StringComparison.OrdinalIgnoreCase)) ?? false;
        var threeDsChallenge = string.Equals(
            order.PaymentSource?.Card?.AuthenticationResult?.ThreeDSecure?.AuthenticationStatus, "C", StringComparison.OrdinalIgnoreCase);

        if (string.Equals(order.Status, "PAYER_ACTION_REQUIRED", StringComparison.OrdinalIgnoreCase) || hasPayerActionLink || threeDsChallenge)
        {
            throw new PaymentChallengeException(
                $"PayPal requires the shopper to complete a browser approval step for order {order.Id} " +
                "(SCA/3DS challenge). This integration only supports direct, headless card payments; " +
                "a manual/browser approval flow is out of scope.");
        }
    }

    private static BillingAddress ToBillingAddress(CardDetails card) => new()
    {
        AddressLine1 = card.AddressLine1,
        AddressLine2 = card.AddressLine2,
        AdminArea2 = card.City,
        AdminArea1 = card.State,
        PostalCode = card.PostalCode,
        CountryCode = card.CountryCode
    };

    private static string FormatAmount(decimal amount, string currency)
    {
        var zeroDecimalCurrencies = new[] { "JPY", "HUF", "TWD" };
        var decimals = Array.IndexOf(zeroDecimalCurrencies, currency.ToUpperInvariant()) >= 0 ? 0 : 2;
        return amount.ToString("F" + decimals, CultureInfo.InvariantCulture);
    }

    private static decimal ParseAmount(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    private static string ToRfc3339(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
}
