using System.Threading;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>The raw body and status captured for one Upvest response on the current async flow.</summary>
public sealed class UpvestCaptureSlot
{
    public string? Body { get; set; }
    public int StatusCode { get; set; }
}

/// <summary>
/// Captures the raw body and status of the last Upvest response on the current async flow. The gateway
/// reads fields (ids, statuses) from this raw body because several of the mock/SDK response models are
/// stricter than the service's 2xx bodies — relying on the captured body keeps the integration working
/// while still invoking the SDK operation (which then throws <c>ResponseDeserializationException</c>,
/// tolerated by the gateway).
///
/// The slot is set by the gateway before an SDK call and mutated by the signing handler on the response.
/// The slot reference flows down into the handler via <see cref="AsyncLocal{T}"/>, and the handler mutates
/// the referenced object (rather than re-assigning the async-local), so the captured value is visible to the
/// gateway after the call returns — or throws.
/// </summary>
public sealed class UpvestResponseCapture
{
    private readonly AsyncLocal<UpvestCaptureSlot?> _current = new();

    public UpvestCaptureSlot Begin()
    {
        var slot = new UpvestCaptureSlot();
        _current.Value = slot;
        return slot;
    }

    public void End() => _current.Value = null;

    public void Record(string body, int statusCode)
    {
        if (_current.Value is { } slot)
        {
            slot.Body = body;
            slot.StatusCode = statusCode;
        }
    }
}
