using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Fails host startup if the Upvest integration cannot be used: the settings are validated by
/// <c>ValidateOnStart</c>, and injecting <see cref="UpvestRequestSigner"/> here forces the signing key to be
/// loaded (and decrypted) before the app serves anything — rather than surfacing as a signature failure on
/// the first call. No secret values are logged.
/// </summary>
public sealed class UpvestStartupValidator : IHostedService
{
    private readonly ILogger<UpvestStartupValidator> _logger;

    public UpvestStartupValidator(UpvestRequestSigner signer, ILogger<UpvestStartupValidator> logger)
    {
        // Constructing the signer (resolved above) has already loaded and decrypted the key.
        _ = signer;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Upvest integration configured and signing key loaded.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
