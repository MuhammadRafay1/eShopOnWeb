using System.Threading;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Carries the raw HTTP response (status + body) of the current Upvest call from the authentication handler
/// back up to the caller. The SDK's generated models enforce <c>required</c> fields strictly, so many of
/// Upvest's 2xx bodies throw <c>ResponseDeserializationException</c> before the gateway can read them; the
/// gateway therefore reads the fields it needs from this captured body instead, deciding success/failure by
/// the captured status code.
///
/// An <see cref="AsyncLocal{T}"/> flows a parent's value DOWN into the handler but a value the handler
/// assigns does not flow back UP — so the caller seeds a mutable <see cref="Box"/> that the handler mutates.
/// </summary>
public sealed class UpvestResponseCapture
{
    private static readonly AsyncLocal<Box?> Current = new();

    /// <summary>Begin capturing for the current logical call. Returns the box the result will be written into.</summary>
    public Box Begin()
    {
        var box = new Box();
        Current.Value = box;
        return box;
    }

    /// <summary>Called by the handler to record the raw response of the outgoing request.</summary>
    public void Record(int statusCode, string body)
    {
        var box = Current.Value;
        if (box is not null)
        {
            box.StatusCode = statusCode;
            box.Body = body;
            box.Recorded = true;
        }
    }

    public sealed class Box
    {
        public bool Recorded { get; set; }
        public int StatusCode { get; set; }
        public string Body { get; set; } = string.Empty;
    }
}
