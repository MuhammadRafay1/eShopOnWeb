using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Exceptions;
using UpvestInvestmentApi.Models;
using UpvestInvestmentApi.Models.Enums;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio;

/// <summary>
/// Drives the five managed-portfolio flows against Upvest through the SDK, keeping the application's own
/// record of investors, models, payments and realignments. Every SDK call goes through
/// <see cref="CallAsync{T}"/>, which is the single boundary that converts SDK failures into
/// <see cref="UpvestApiException"/>; no personal data is ever logged.
/// </summary>
public partial class UpvestPortfolioService : IUpvestPortfolioService
{
    private readonly UpvestInvestmentApiClient _client;
    private readonly IPortfolioRepository _repository;
    private readonly UpvestSettings _settings;
    private readonly ILogger<UpvestPortfolioService> _logger;

    public UpvestPortfolioService(
        UpvestInvestmentApiClient client,
        IPortfolioRepository repository,
        IOptions<UpvestSettings> settings,
        ILogger<UpvestPortfolioService> logger)
    {
        _client = client;
        _repository = repository;
        _settings = settings.Value;
        _logger = logger;
    }

    // ---------------------------------------------------------------------------------------------
    // SDK error boundary
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Runs one SDK call and translates every SDK failure into <see cref="UpvestApiException"/>, carrying the
    /// provider status where one was received. Logs the operation, HTTP verb, URI and status only — never a
    /// request body, so personal data never reaches the logs.
    /// </summary>
    private async Task<T> CallAsync<T>(string operation, Func<CancellationToken, Task<T>> call, CancellationToken ct)
    {
        try
        {
            return await call(ct);
        }
        catch (ResponseDeserializationException ex)
        {
            _logger.LogWarning("Upvest {Operation} returned an unprocessable response: {Method} {Uri} -> {Status}",
                operation, ex.Method, ex.RequestUri, (int)ex.StatusCode);
            throw new UpvestApiException($"Upvest {operation} returned a response that could not be processed.",
                (int)ex.StatusCode, ex);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning("Upvest {Operation} failed: {Method} {Uri} -> {Status}",
                operation, ex.Method, ex.RequestUri, (int)ex.StatusCode);
            throw new UpvestApiException($"Upvest {operation} failed.", (int)ex.StatusCode, ex);
        }
        catch (AuthSchemeException ex)
        {
            _logger.LogError(ex, "Upvest {Operation} could not authenticate (credentials missing or rejected).", operation);
            throw new UpvestApiException("Upvest credentials are missing or were rejected.", null, ex);
        }
        catch (SdkTimeoutException ex)
        {
            _logger.LogWarning("Upvest {Operation} timed out after {Timeout}.", operation, ex.Timeout);
            throw new UpvestApiException("Upvest did not respond in time.", null, ex);
        }
        catch (SdkConnectionException ex)
        {
            _logger.LogWarning(ex, "Upvest {Operation} could not reach the provider.", operation);
            throw new UpvestApiException("Upvest is unreachable.", null, ex);
        }
    }

    /// <summary>Overload for SDK calls that return no body (<see cref="Task"/>).</summary>
    private async Task CallAsync(string operation, Func<CancellationToken, Task> call, CancellationToken ct) =>
        await CallAsync<bool>(operation, async c => { await call(c); return true; }, ct);

    /// <summary>True when the failure is our own authorization/scope problem (not the caller's fault).</summary>
    private static bool IsAuthorizationFailure(UpvestApiException ex) =>
        ex.ProviderStatusCode is 401 or 403;

    // ---------------------------------------------------------------------------------------------
    // Shared helpers
    // ---------------------------------------------------------------------------------------------

    /// <summary>A deterministic idempotency key derived from a stable seed, so an SDK/caller retry of the
    /// same logical write reuses the same key and reaches Upvest at most once.</summary>
    private static Guid DeterministicKey(string seed)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(seed));
        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, 16);
        return new Guid(guidBytes);
    }

    private string Currency => _settings.Payments.Currency;

    private UpvestInvestmentApi.Models.Enums.Currency CurrencyEnum =>
        UpvestInvestmentApi.Models.Enums.Currency.TryGetKnownValue(Currency, out var c)
            ? c
            : UpvestInvestmentApi.Models.Enums.Currency.Eur;

    /// <summary>Format a money amount as the decimal string Upvest expects (two decimal places).</summary>
    private static string FormatMoney(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Format a weight percentage to match Upvest's weight pattern (<c>100</c> or up to 5 dp).</summary>
    private static string FormatWeight(decimal weight) =>
        weight == 100m ? "100" : weight.ToString("0.#####", CultureInfo.InvariantCulture);

    /// <summary>Parse a decimal Upvest sends as a string; returns 0 when absent or unparseable.</summary>
    private static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
}
