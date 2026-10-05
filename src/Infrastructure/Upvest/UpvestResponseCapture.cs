using System.Threading;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Captures the raw JSON body of the most recent Upvest response within the current async flow. The mock (and
/// in general any provider stricter-or-looser than the generated models) can return success bodies that omit
/// members the SDK marks <c>required</c>, which makes typed deserialization throw. The gateway reads the
/// captured body to extract the few fields it needs (ids and statuses, by their documented wire names) while
/// still issuing every call through the SDK client and its signing handler.
/// </summary>
public sealed class UpvestResponseCapture
{
    private readonly AsyncLocal<ResponseHolder?> _current = new();

    /// <summary>Start capturing for the next call in this async flow; returns the holder to read afterwards.</summary>
    public ResponseHolder Begin()
    {
        var holder = new ResponseHolder();
        _current.Value = holder;
        return holder;
    }

    /// <summary>The holder for the current async flow, if capture is active (read by the handler).</summary>
    public ResponseHolder? Current => _current.Value;
}

/// <summary>Mutable holder so a write by the delegating handler (deeper in the call stack) is visible to the gateway.</summary>
public sealed class ResponseHolder
{
    public string? Body { get; set; }
    public int StatusCode { get; set; }
}
