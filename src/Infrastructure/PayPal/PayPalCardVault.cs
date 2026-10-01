using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Payments;
using PayPalServerSdk;
using PayPalServerSdk.Core.ErrorResponse;
using PayPalServerSdk.Core.Exceptions;
using PayPalServerSdk.Errors;
using PayPalServerSdk.Models;
using PayPalServerSdk.Models.Enums;
using PayPalServerSdk.Requests.Vault;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// PayPal Vault implementation of <see cref="ICardVault"/>. Saving a card is a two-step, browser-less flow:
/// create a setup token for the raw card, then exchange it for a permanent payment token (the vaulted card).
/// Full card data is sent to PayPal, never stored here.
/// </summary>
public sealed class PayPalCardVault : ICardVault
{
    private static readonly TimeSpan CallBudget = TimeSpan.FromSeconds(30);

    private readonly PayPalServerSdkClient _client;
    private readonly IAppLogger<PayPalCardVault> _logger;

    public PayPalCardVault(PayPalServerSdkClient client, IAppLogger<PayPalCardVault> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<SavedCardResult> SaveCardAsync(CardDetails card, string buyerReference, CancellationToken cancellationToken)
    {
        var setupToken = await CreateSetupTokenAsync(card, buyerReference, cancellationToken);
        return await ExchangeForPaymentTokenAsync(setupToken, buyerReference, cancellationToken);
    }

    private async Task<string> CreateSetupTokenAsync(CardDetails card, string buyerReference, CancellationToken cancellationToken)
    {
        var request = new CreateSetupTokenRequest
        {
            // Fresh key per save: saving the same physical card twice (e.g. under two aliases) is legitimate.
            PayPalRequestId = Guid.NewGuid().ToString("N"),
            Body = new SetupTokenRequest
            {
                PaymentSource = new SetupTokenRequestPaymentSource
                {
                    Card = new SetupTokenRequestCard
                    {
                        Name = card.Name,
                        Number = card.Number,
                        Expiry = card.Expiry,
                        SecurityCode = card.SecurityCode,
                        BillingAddress = BuildAddress(card.BillingAddress)
                    }
                }
            }
        };

        try
        {
            var setup = await Bounded(ct => _client.Vault.CreateSetupToken(request, cancellationToken: ct), cancellationToken);

            if (setup.Status is { } status && status == PaymentTokenStatus.PayerActionRequired)
            {
                // The card needs browser approval (e.g. 3DS). Per the task, stop and report rather than build a round-trip.
                throw new PayerActionRequiredException();
            }
            if (setup.Id is null)
            {
                throw new PaymentGatewayException("PayPal did not return a setup token for the card.", null);
            }
            return setup.Id;
        }
        catch (ApiException<CreateSetupTokenError> ex)
        {
            throw TranslateTyped("save the card", ex, ex.Error.TryGetError(out var err) ? err : null);
        }
        catch (Exception ex) when (IsTransportOrShape(ex))
        {
            throw Translate(ex);
        }
    }

    private async Task<SavedCardResult> ExchangeForPaymentTokenAsync(string setupTokenId, string buyerReference, CancellationToken cancellationToken)
    {
        var request = new CreatePaymentTokenRequest
        {
            PayPalRequestId = Guid.NewGuid().ToString("N"),
            Body = new PaymentTokenRequest
            {
                PaymentSource = new PaymentTokenRequestPaymentSource
                {
                    Token = new VaultTokenRequest { Id = setupTokenId, Type = VaultTokenRequestType.SetupToken }
                }
            }
        };

        try
        {
            var response = await Bounded(ct => _client.Vault.CreatePaymentToken(request, cancellationToken: ct), cancellationToken);
            var saved = response.PaymentSource?.Card;
            return new SavedCardResult(
                response.Id!,
                saved?.LastDigits,
                saved?.Brand?.Value,
                saved?.Expiry);
        }
        catch (ApiException<CreatePaymentTokenError> ex)
        {
            throw TranslateTyped("save the card", ex, ex.Error.TryGetError(out var err) ? err : null);
        }
        catch (Exception ex) when (IsTransportOrShape(ex))
        {
            throw Translate(ex);
        }
    }

    public async Task DeletePaymentTokenAsync(string paymentTokenId, CancellationToken cancellationToken)
    {
        try
        {
            await Bounded(async ct =>
            {
                await _client.Vault.DeletePaymentToken(new DeletePaymentTokenRequest { Id = paymentTokenId }, cancellationToken: ct);
                return true;
            }, cancellationToken);
        }
        catch (ApiException<DeletePaymentTokenError> ex)
        {
            if ((int)ex.StatusCode == 404)
            {
                _logger.LogInformation("Vault delete of {0} returned 404 — already gone, idempotent success.", paymentTokenId);
                return;
            }
            throw TranslateTyped("delete the card", ex, ex.Error.TryGetError(out var err) ? err : null);
        }
        catch (ApiException ex) when (ex is not ApiException<DeletePaymentTokenError> && (int)ex.StatusCode == 404)
        {
            // A 404 may surface only via the raw-error path on this operation — still idempotent success.
            _logger.LogInformation("Vault delete of {0} returned 404 — already gone, idempotent success.", paymentTokenId);
            return;
        }
        catch (Exception ex) when (IsTransportOrShape(ex))
        {
            throw Translate(ex);
        }
    }

    private static PayPalServerSdk.Models.Address? BuildAddress(CardBillingAddress? address)
    {
        if (address is null)
        {
            return null;
        }
        return new PayPalServerSdk.Models.Address
        {
            AddressLine1 = address.AddressLine1,
            AddressLine2 = address.AddressLine2,
            AdminArea2 = address.AdminArea2,
            AdminArea1 = address.AdminArea1,
            PostalCode = address.PostalCode,
            CountryCode = address.CountryCode
        };
    }

    private PaymentGatewayException TranslateTyped(string action, ApiException ex, Error? error)
    {
        if (error is not null)
        {
            _logger.LogWarning("Vault could not {0}: {1} (debug_id={2})", action, error.Name, error.DebugId);
            return new PaymentGatewayException($"PayPal could not {action}: {error.Name} - {error.Message}", (int)ex.StatusCode, error.DebugId, ex);
        }
        return new PaymentGatewayException($"PayPal could not {action} (HTTP {(int)ex.StatusCode}).", (int)ex.StatusCode, inner: ex);
    }

    private static bool IsTransportOrShape(Exception ex) =>
        ex is ResponseDeserializationException or SdkTimeoutException or SdkConnectionException or AuthSchemeException;

    private PaymentGatewayException Translate(Exception ex) => ex switch
    {
        ResponseDeserializationException rde => new PaymentGatewayException("PayPal returned a response that could not be processed.", (int)rde.StatusCode, inner: rde),
        SdkTimeoutException te => new PaymentGatewayException("PayPal did not respond in time.", null, inner: te),
        SdkConnectionException ce => new PaymentGatewayException("PayPal is currently unreachable.", null, inner: ce),
        AuthSchemeException ae => new PaymentGatewayException("PayPal credentials were rejected.", null, inner: ae),
        _ => new PaymentGatewayException("An unexpected PayPal error occurred.", null, inner: ex)
    };

    private static async Task<T> Bounded<T>(Func<CancellationToken, Task<T>> call, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(CallBudget);
        return await call(cts.Token);
    }
}
