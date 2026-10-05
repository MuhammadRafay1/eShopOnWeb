using System.Net;
using System.Threading;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Carries the raw HTTP status + body of the Upvest response for the current logical call back to the
/// gateway. Needed because several Upvest 2xx response bodies omit members the generated SDK models mark
/// <c>required</c>, so the SDK's typed deserialization throws; the gateway reads the fields it needs
/// (id, status) from this captured body instead.
///
/// The caller opens a <see cref="Slot"/> before the SDK call (which flows down the async context to the
/// handler); the handler mutates that same slot, so the result is visible to the caller afterwards.
/// </summary>
public sealed class UpvestResponseCapture
{
    public sealed class Slot
    {
        public HttpStatusCode? Status { get; set; }
        public string Body { get; set; } = string.Empty;
        public string? RequestId { get; set; }
    }

    private readonly AsyncLocal<Slot?> _current = new();

    public Slot Begin()
    {
        var slot = new Slot();
        _current.Value = slot;
        return slot;
    }

    public void Record(HttpStatusCode status, string body, string? requestId)
    {
        var slot = _current.Value;
        if (slot is null) return;
        slot.Status = status;
        slot.Body = body;
        slot.RequestId = requestId;
    }
}
