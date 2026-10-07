using System;
using System.Threading;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Lets the gateway read the raw status + JSON body of an Upvest response even
/// when the SDK fails to deserialize it into its generated model. The signing
/// handler records each response into an <see cref="AsyncLocal{T}"/> holder the
/// gateway opens around a call with <see cref="Capture"/>. The holder is
/// per-async-flow, so concurrent calls never cross-contaminate.
/// </summary>
public static class UpvestRawResponse
{
    internal sealed class Holder { public int? StatusCode; public string? Body; }

    private static readonly AsyncLocal<Holder?> Slot = new();

    /// <summary>Open a capture scope. Read <see cref="Scope"/> after the call.</summary>
    public static Scope Capture()
    {
        var holder = new Holder();
        Slot.Value = holder;
        return new Scope(holder);
    }

    /// <summary>Called by the signing handler to record the current response.</summary>
    internal static void Record(int statusCode, string body)
    {
        var holder = Slot.Value;
        if (holder != null) { holder.StatusCode = statusCode; holder.Body = body; }
    }

    public sealed class Scope : IDisposable
    {
        private readonly Holder _holder;
        internal Scope(Holder holder) => _holder = holder;

        /// <summary>HTTP status of the last Upvest response seen in this scope.</summary>
        public int? StatusCode => _holder.StatusCode;

        /// <summary>Body of the last Upvest response seen in this scope, or null.</summary>
        public string? Body => _holder.Body;

        public void Dispose() => Slot.Value = null;
    }
}
