using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;

namespace Microsoft.eShopWeb.Infrastructure.Payments.PayPal;

/// <summary>
/// Authorize/capture/reauthorize/void/refund against PayPal Orders v2 and Payments v2.
/// </summary>
public class PayPalOrdersGateway : IPayPalPaymentGateway
{
    private readonly PayPalApiClient _api;

    public PayPalOrdersGateway(PayPalApiClient api)
    {
        _api = api;
    }

    public async Task<PayPalAuthorizationOutcome> AuthorizeAsync(PayPalAuthorizeRequest request, CancellationToken ct)
    {
        var card = BuildCard(request);
        var wire = new CreateOrderWire
        {
            Intent = "AUTHORIZE",
            PurchaseUnits = new List<PurchaseUnitWire>
            {
                new()
                {
                    InvoiceId = request.InvoiceId,
                    Amount = new AmountWire
                    {
                        CurrencyCode = request.Currency,
                        Value = FormatAmount(request.Amount)
                    }
                }
            },
            PaymentSource = new PaymentSourceWire { Card = card }
        };

        var response = await _api.PostAsync<OrderResponseWire>(
            "v2/checkout/orders", wire, request.IdempotencyKey, ct, preferRepresentation: true);

        if (!response.IsSuccess)
        {
            // A 4xx here is a business non-approval (e.g. an instrument decline), not a fault.
            return new PayPalAuthorizationOutcome
            {
                Approved = false,
                PayPalOrderId = string.Empty,
                Status = PaymentAuthorizationStatus.Denied,
                DeclineReason = response.Error?.Describe() ?? "PayPal declined the payment."
            };
        }

        var order = response.Value
            ?? throw new PayPalIntegrationException("PayPal returned an empty order response.");

        // Hard stop: PayPal is asking for a shopper-facing approval/challenge.
        if (string.Equals(order.Status, "PAYER_ACTION_REQUIRED", StringComparison.OrdinalIgnoreCase))
        {
            throw new PayPalApprovalRequiredException(
                $"PayPal returned PAYER_ACTION_REQUIRED for order {request.InvoiceId}; a shopper-facing approval is not supported for this integration.");
        }

        var authorization = order.PurchaseUnits?
            .FirstOrDefault()?.Payments?.Authorizations?.FirstOrDefault();

        if (authorization is null || string.IsNullOrEmpty(authorization.Id))
        {
            // No authorization object came back and it wasn't a payer-action state; treat as decline.
            return new PayPalAuthorizationOutcome
            {
                Approved = false,
                PayPalOrderId = order.Id ?? string.Empty,
                Status = PaymentAuthorizationStatus.Denied,
                DeclineReason = $"PayPal did not return an authorization (order status {order.Status})."
            };
        }

        var reason = authorization.StatusDetails?.Reason;
        if (!string.IsNullOrEmpty(reason) &&
            (reason.Equals("PENDING_REVIEW", StringComparison.OrdinalIgnoreCase) ||
             reason.Equals("DECLINED_BY_RISK_FRAUD_FILTERS", StringComparison.OrdinalIgnoreCase)))
        {
            throw new PayPalApprovalRequiredException(
                $"PayPal flagged authorization for order {request.InvoiceId} as '{reason}', which requires manual/shopper action not supported by this integration.");
        }

        var status = MapAuthorizationStatus(authorization.Status);
        if (status == PaymentAuthorizationStatus.Denied)
        {
            return new PayPalAuthorizationOutcome
            {
                Approved = false,
                PayPalOrderId = order.Id ?? string.Empty,
                Status = status,
                DeclineReason = reason ?? "PayPal denied the authorization."
            };
        }

        return new PayPalAuthorizationOutcome
        {
            Approved = true,
            PayPalOrderId = order.Id ?? string.Empty,
            AuthorizationId = authorization.Id,
            Status = status,
            ExpiresAt = authorization.ExpirationTime ?? DateTimeOffset.UtcNow.AddDays(29)
        };
    }

    public async Task<PayPalCaptureOutcome> CaptureAsync(string authorizationId, decimal amount,
        string currency, string orderIdForInvoice, string idempotencyKey, CancellationToken ct)
    {
        var body = new
        {
            amount = new AmountWire { CurrencyCode = currency, Value = FormatAmount(amount) },
            final_capture = true,
            invoice_id = orderIdForInvoice
        };

        var response = await _api.PostAsync<CaptureResponseWire>(
            $"v2/payments/authorizations/{authorizationId}/capture", body, idempotencyKey, ct,
            preferRepresentation: true);

        if (!response.IsSuccess)
        {
            var expired = response.Error?.HasIssue("AUTHORIZATION_EXPIRED") == true;
            return new PayPalCaptureOutcome
            {
                Result = expired ? PayPalCaptureResult.AuthorizationExpired : PayPalCaptureResult.Failed,
                FailureIssue = response.Error?.PrimaryIssue,
                FailureDescription = response.Error?.Describe()
            };
        }

        var capture = response.Value
            ?? throw new PayPalIntegrationException("PayPal returned an empty capture response.");
        var breakdown = capture.SellerReceivableBreakdown;

        return new PayPalCaptureOutcome
        {
            Result = PayPalCaptureResult.Completed,
            CaptureId = capture.Id,
            Status = MapCaptureStatus(capture.Status),
            GrossAmount = ParseMoney(breakdown?.GrossAmount),
            FeeAmount = ParseMoney(breakdown?.PaypalFee),
            NetAmount = ParseMoney(breakdown?.NetAmount)
        };
    }

    public async Task<PayPalReauthorizeOutcome> ReauthorizeAsync(string authorizationId, decimal amount,
        string currency, CancellationToken ct)
    {
        var body = new
        {
            amount = new AmountWire { CurrencyCode = currency, Value = FormatAmount(amount) }
        };

        var response = await _api.PostAsync<AuthorizationWire>(
            $"v2/payments/authorizations/{authorizationId}/reauthorize", body, idempotencyKey: null, ct,
            preferRepresentation: true);

        if (!response.IsSuccess)
        {
            return new PayPalReauthorizeOutcome
            {
                Result = PayPalReauthorizeResult.Failed,
                FailureIssue = response.Error?.PrimaryIssue,
                FailureDescription = response.Error?.Describe()
            };
        }

        var auth = response.Value
            ?? throw new PayPalIntegrationException("PayPal returned an empty reauthorize response.");

        return new PayPalReauthorizeOutcome
        {
            Result = PayPalReauthorizeResult.Renewed,
            Status = MapAuthorizationStatus(auth.Status),
            ExpiresAt = auth.ExpirationTime ?? DateTimeOffset.UtcNow.AddDays(29)
        };
    }

    public async Task VoidAsync(string authorizationId, CancellationToken ct)
    {
        var response = await _api.PostAsync<object>(
            $"v2/payments/authorizations/{authorizationId}/void", body: null, idempotencyKey: null, ct);

        if (!response.IsSuccess)
        {
            throw new PayPalIntegrationException(
                $"PayPal could not void authorization {authorizationId}: {response.Error?.Describe()}");
        }
    }

    public async Task<PayPalRefundOutcome> RefundAsync(string captureId, decimal? amount, string currency,
        string idempotencyKey, CancellationToken ct)
    {
        object? body = amount.HasValue
            ? new { amount = new AmountWire { CurrencyCode = currency, Value = FormatAmount(amount.Value) } }
            : null;

        var response = await _api.PostAsync<RefundResponseWire>(
            $"v2/payments/captures/{captureId}/refund", body, idempotencyKey, ct);

        if (!response.IsSuccess)
        {
            throw new PayPalIntegrationException(
                $"PayPal could not refund capture {captureId}: {response.Error?.Describe()}");
        }

        var refund = response.Value
            ?? throw new PayPalIntegrationException("PayPal returned an empty refund response.");
        if (string.IsNullOrEmpty(refund.Id))
        {
            throw new PayPalIntegrationException("PayPal refund response did not contain a refund id.");
        }

        return new PayPalRefundOutcome
        {
            RefundId = refund.Id,
            Status = refund.Status ?? "PENDING"
        };
    }

    private static CardWire BuildCard(PayPalAuthorizeRequest request)
    {
        if (request.VaultId is not null)
        {
            return new CardWire
            {
                VaultId = request.VaultId,
                StoredCredential = new StoredCredentialWire
                {
                    PaymentInitiator = "CUSTOMER",
                    PaymentType = "UNSCHEDULED",
                    Usage = "SUBSEQUENT"
                }
            };
        }

        var card = request.Card
            ?? throw new PayPalIntegrationException("Authorize request had neither a card nor a vault id.");

        return new CardWire
        {
            Name = card.Name,
            Number = card.Number,
            Expiry = card.Expiry,
            SecurityCode = card.SecurityCode,
            BillingAddress = ToBillingAddress(card.BillingAddress)
        };
    }

    internal static BillingAddressWire? ToBillingAddress(CardBillingAddress? address)
    {
        if (address is null)
        {
            return null;
        }
        return new BillingAddressWire
        {
            AddressLine1 = address.AddressLine1,
            AdminArea2 = address.City,
            AdminArea1 = address.State,
            PostalCode = address.PostalCode,
            CountryCode = address.CountryCode
        };
    }

    internal static string FormatAmount(decimal amount) =>
        amount.ToString("F2", CultureInfo.InvariantCulture);

    private static decimal ParseMoney(MoneyWire? money)
    {
        if (money?.Value is null)
        {
            return 0m;
        }
        return decimal.TryParse(money.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0m;
    }

    private static PaymentAuthorizationStatus MapAuthorizationStatus(string? status) =>
        status?.ToUpperInvariant() switch
        {
            "CREATED" => PaymentAuthorizationStatus.Created,
            "CAPTURED" => PaymentAuthorizationStatus.Captured,
            "DENIED" => PaymentAuthorizationStatus.Denied,
            "PARTIALLY_CAPTURED" => PaymentAuthorizationStatus.PartiallyCaptured,
            "VOIDED" => PaymentAuthorizationStatus.Voided,
            "PENDING" => PaymentAuthorizationStatus.Pending,
            _ => PaymentAuthorizationStatus.Pending
        };

    private static PaymentCaptureStatus MapCaptureStatus(string? status) =>
        status?.ToUpperInvariant() switch
        {
            "COMPLETED" => PaymentCaptureStatus.Completed,
            "DECLINED" => PaymentCaptureStatus.Declined,
            "PARTIALLY_REFUNDED" => PaymentCaptureStatus.PartiallyRefunded,
            "PENDING" => PaymentCaptureStatus.Pending,
            "REFUNDED" => PaymentCaptureStatus.Refunded,
            "FAILED" => PaymentCaptureStatus.Failed,
            _ => PaymentCaptureStatus.Pending
        };
}
