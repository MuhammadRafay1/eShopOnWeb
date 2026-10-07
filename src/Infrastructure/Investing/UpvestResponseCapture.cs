using System.Threading;
using UpvestInvestmentApi.Standard.Http.Client;
using UpvestInvestmentApi.Standard.Http.Response;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Captures the raw HTTP response of each Upvest call. The vendored SDK's generated response models
/// are stricter than the responses the Upvest sandbox actually returns (required fields it omits,
/// oneOf unions that match nothing), so deserialising the typed <c>Data</c> throws even when the call
/// succeeded. This callback fires before that deserialisation, so the gateway can read the real body
/// from here and ignore the typed-model mismatch.
///
/// The captured response is held in an <see cref="AsyncLocal{T}"/>, so concurrent calls on the shared
/// (singleton) client each see only their own response.
/// </summary>
public sealed class UpvestResponseCapture : HttpCallback
{
    public sealed class Captured
    {
        public int StatusCode { get; set; }
        public string? Body { get; set; }
    }

    private static readonly AsyncLocal<Captured?> Current = new();

    /// <summary>Begins capturing for the current async flow. Call immediately before an SDK operation.</summary>
    public static Captured Begin()
    {
        var captured = new Captured();
        Current.Value = captured;
        return captured;
    }

    public override void OnAfterResponse(HttpResponse response)
    {
        var captured = Current.Value;
        if (captured is null) return;
        captured.StatusCode = response.StatusCode;
        captured.Body = response.Body;
    }
}
