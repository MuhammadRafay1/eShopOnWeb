using System.Net.Http;
using UpvestInvestmentApi.Standard;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The single, long-lived Upvest connection: the generated SDK client plus the one
/// <see cref="HttpClient"/> that carries the shared signing <see cref="UpvestSigningHandler"/>.
/// Both route through the same handler, so every call — SDK or direct — is signed and sent to the
/// configured <c>Upvest:BaseUrl</c>.
/// </summary>
public sealed class UpvestConnection
{
    public UpvestConnection(UpvestInvestmentApiClient client, HttpClient signedHttpClient)
    {
        Client = client;
        SignedHttpClient = signedHttpClient;
    }

    /// <summary>The SDK client. Drives OAuth token acquisition and most operations.</summary>
    public UpvestInvestmentApiClient Client { get; }

    /// <summary>
    /// The signed HTTP client. Used only for the handful of account/account-group operations whose
    /// generated SDK response models are incompatible with the Upvest sandbox's responses (the SDK
    /// requires a <c>users</c> array the sandbox does not return). Requests still follow the
    /// SDK-documented contract and are signed by the shared handler; the OAuth token is taken from the
    /// SDK's auth manager.
    /// </summary>
    public HttpClient SignedHttpClient { get; }
}
