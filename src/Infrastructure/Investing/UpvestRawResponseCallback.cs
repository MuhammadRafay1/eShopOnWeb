using UpvestInvestmentApi.Standard.Http.Client;
using UpvestInvestmentApi.Standard.Http.Response;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Records the raw body of the most recent Upvest response. A few of the SDK's generated response
/// models do not match this server's response shape (they require a <c>users</c> array where the server
/// returns <c>user_id</c>), so those calls throw while deserialising even though the call succeeded and
/// the body is valid JSON. This callback lets the gateway read the id straight from the body the SDK
/// received, without making any call of its own.
/// </summary>
public sealed class UpvestRawResponseCallback : HttpCallback
{
    public string? LastBody { get; private set; }

    public override void OnAfterResponse(HttpResponse response)
    {
        LastBody = response?.Body;
    }
}
