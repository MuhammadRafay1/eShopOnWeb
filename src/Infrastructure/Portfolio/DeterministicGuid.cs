using System;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio;

/// <summary>
/// Produces stable, name-based GUIDs (RFC 4122 v5-style, SHA-1) so a retried write replays the
/// same Upvest idempotency key instead of acting twice. The key is derived from values the shop
/// already holds (a payment/realignment id plus a step name) — never from <see cref="Guid.NewGuid"/>.
/// </summary>
public static class DeterministicGuid
{
    // A fixed namespace for all eShopOnWeb managed-portfolio idempotency keys.
    private static readonly Guid Namespace = new("8f1d0b7e-2c4a-4e3b-9a6f-1d2c3b4a5e6f");

    public static Guid Create(string name) => Create(Namespace, name);

    public static Guid Create(Guid namespaceId, string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        Span<byte> namespaceBytes = stackalloc byte[16];
        namespaceId.TryWriteBytes(namespaceBytes);
        SwapByteOrder(namespaceBytes); // GUID → big-endian network order

        var nameBytes = Encoding.UTF8.GetBytes(name);

        byte[] hash;
        using (var sha1 = SHA1.Create())
        {
            sha1.TransformBlock(namespaceBytes.ToArray(), 0, 16, null, 0);
            sha1.TransformFinalBlock(nameBytes, 0, nameBytes.Length);
            hash = sha1.Hash!;
        }

        Span<byte> result = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(result);

        // Version 5 (name-based, SHA-1) and RFC 4122 variant.
        result[6] = (byte)((result[6] & 0x0F) | 0x50);
        result[8] = (byte)((result[8] & 0x3F) | 0x80);

        SwapByteOrder(result); // big-endian → GUID byte order
        return new Guid(result);
    }

    private static void SwapByteOrder(Span<byte> guid)
    {
        Swap(guid, 0, 3);
        Swap(guid, 1, 2);
        Swap(guid, 4, 5);
        Swap(guid, 6, 7);
    }

    private static void Swap(Span<byte> array, int a, int b) => (array[a], array[b]) = (array[b], array[a]);
}
