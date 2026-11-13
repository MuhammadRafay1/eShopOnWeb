using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Microsoft.Extensions.Logging;
using UpvestInvestmentApi.Core.Exceptions;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio.Services;

/// <summary>
/// Single boundary that turns every SDK failure into a domain exception, and keeps provider detail and
/// personal data out of logs. Reads translate a lost connection to "provider unavailable"; writes translate
/// it to an <see cref="PortfolioUnknownOutcomeException"/> so the caller is never told a write failed when it
/// may have landed — the status GET endpoints reconcile it by re-reading Upvest.
/// </summary>
internal static class UpvestCall
{
    public static Task<T> ReadAsync<T>(Func<Task<T>> call, string operation, ILogger logger) =>
        InvokeAsync(call, operation, logger, isWrite: false);

    public static Task<T> WriteAsync<T>(Func<Task<T>> call, string operation, ILogger logger) =>
        InvokeAsync(call, operation, logger, isWrite: true);

    private static async Task<T> InvokeAsync<T>(Func<Task<T>> call, string operation, ILogger logger, bool isWrite)
    {
        try
        {
            return await call();
        }
        catch (ApiException ex)
        {
            // Covers ApiException<TError> (typed and RawError) and ResponseDeserializationException.
            // Status is always present; log only the status + operation, never the body (may carry PII).
            logger.LogWarning("Upvest {Operation} returned status {StatusCode}.", operation, (int?)ex.StatusCode);
            throw new PortfolioProviderException(SafeMessage(ex.StatusCode), ex.StatusCode, ex);
        }
        catch (AuthSchemeException ex)
        {
            // Our credential is missing/rejected — never the caller's fault. Log the inner cause only.
            logger.LogError(ex, "Upvest {Operation} could not authenticate (credential problem).", operation);
            throw new PortfolioProviderException("The investment provider is currently unavailable.", null, ex);
        }
        catch (SdkTimeoutException ex)
        {
            logger.LogWarning("Upvest {Operation} timed out after {Timeout}.", operation, ex.Timeout);
            if (isWrite)
                throw new PortfolioUnknownOutcomeException(
                    "The investment provider did not confirm the outcome in time; it will be reconciled.", ex);
            throw new PortfolioProviderException("The investment provider did not respond in time.", null, ex);
        }
        catch (SdkConnectionException ex)
        {
            logger.LogWarning(ex, "Upvest {Operation} could not reach the provider.", operation);
            if (isWrite)
                throw new PortfolioUnknownOutcomeException(
                    "The investment provider could not be reached; the outcome will be reconciled.", ex);
            throw new PortfolioProviderException("The investment provider could not be reached.", null, ex);
        }
    }

    private static string SafeMessage(HttpStatusCode? status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "The investment provider is currently unavailable.",
        HttpStatusCode.TooManyRequests => "The investment provider is temporarily unavailable.",
        >= (HttpStatusCode)400 and < (HttpStatusCode)500 => "The investment provider rejected the request.",
        _ => "The investment provider is currently unavailable."
    };
}
