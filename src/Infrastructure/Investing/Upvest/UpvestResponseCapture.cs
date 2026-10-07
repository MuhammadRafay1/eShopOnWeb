using System.Runtime.CompilerServices;
using System.Threading;
using UpvestInvestmentApi.Standard.Http.Client;
using UpvestInvestmentApi.Standard.Http.Response;

namespace Microsoft.eShopWeb.Infrastructure.Investing.Upvest;

/// <summary>
/// Captures the raw JSON body of the last Upvest response on the current async flow. Needed because a
/// few of this SDK version's response models (account / account-group, which expect a <c>users</c>
/// array) do not match the provider's current payloads (which carry a single <c>user_id</c>), so the
/// SDK throws while deserializing even though the HTTP call succeeded. The call still happens through
/// the SDK; this only lets the gateway read the id the typed model could not surface.
///
/// An <see cref="AsyncLocal{T}"/> box makes capture correct under concurrency: each call flow installs
/// its own box before the call, and the callback (which runs within that flow) writes into it.
/// </summary>
public sealed class UpvestResponseCapture : HttpCallback
{
    private static readonly AsyncLocal<StrongBox<string?>> Current = new();

    /// <summary>Start capturing for the current async flow; call immediately before an SDK operation.</summary>
    public void Begin() => Current.Value = new StrongBox<string?>(null);

    /// <summary>The raw body of the last response captured on this flow, or null.</summary>
    public string? LastBody => Current.Value?.Value;

    public override void OnAfterResponse(HttpResponse response)
    {
        var box = Current.Value;
        if (box is not null)
        {
            box.Value = response?.Body;
        }
    }
}
