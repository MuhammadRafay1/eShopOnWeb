using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Verifies the HTTP Message Signature on an inbound Upvest webhook delivery against Upvest's published
/// webhook keys (JWKS). Defence-in-depth: the application re-reads authoritative state from Upvest on any
/// event, so verification failure is logged rather than trusted blindly, but a valid signature confirms the
/// delivery is genuinely from Upvest.
/// </summary>
public sealed class UpvestWebhookVerifier
{
    private readonly IUpvestGateway _gateway;
    private volatile UpvestWebhookKey[]? _keys;

    public UpvestWebhookVerifier(IUpvestGateway gateway) => _gateway = gateway;

    public async Task<bool> VerifyAsync(
        string method, string path, IReadOnlyDictionary<string, string> headers, byte[] body, CancellationToken ct)
    {
        try
        {
            if (!headers.TryGetValue("signature-input", out var signatureInput) ||
                !headers.TryGetValue("signature", out var signature))
                return false;

            var trimmed = signatureInput.Trim();
            if (!trimmed.StartsWith("sig1=", StringComparison.Ordinal)) return false;
            var signatureParams = trimmed.Substring("sig1=".Length); // "(...);created=...;keyid=..."

            var m = Regex.Match(signatureParams, @"^\(([^)]*)\)(.*)$");
            if (!m.Success) return false;
            var comps = Regex.Matches(m.Groups[1].Value, "\"([^\"]+)\"").Select(x => x.Groups[1].Value).ToList();

            var keyIdMatch = Regex.Match(m.Groups[2].Value, "keyid=\"([^\"]+)\"");
            if (!keyIdMatch.Success) return false;
            var keyId = keyIdMatch.Groups[1].Value;

            var sigMatch = Regex.Match(signature, @"sig1=:([A-Za-z0-9+/=]+):");
            if (!sigMatch.Success) return false;
            var sigBytes = Convert.FromBase64String(sigMatch.Groups[1].Value);

            // Optional body-digest check.
            if (headers.TryGetValue("digest", out var digest))
            {
                var expected = "SHA-256=" + Convert.ToBase64String(SHA256.HashData(body));
                if (!string.Equals(digest.Trim(), expected, StringComparison.Ordinal))
                    return false;
            }

            var lines = new List<string>();
            foreach (var c in comps)
            {
                string value = c switch
                {
                    "@method" => method,
                    "@path" => path,
                    _ => headers.TryGetValue(c, out var hv) ? hv : null!
                };
                if (value is null) return false;
                lines.Add($"\"{c}\": {value}");
            }
            var baseString = string.Join("\n", lines) + $"\n\"@signature-params\": {signatureParams}";

            var keys = _keys ??= await _gateway.GetWebhookKeysAsync(ct).ConfigureAwait(false);
            var key = Array.Find(keys, k => k.Kid == keyId);
            if (key is null)
            {
                // Key set may have rotated; refresh once.
                _keys = await _gateway.GetWebhookKeysAsync(ct).ConfigureAwait(false);
                key = Array.Find(_keys, k => k.Kid == keyId);
                if (key is null) return false;
            }

            using var ecdsa = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP521,
                Q = new ECPoint { X = Base64UrlDecode(key.X), Y = Base64UrlDecode(key.Y) },
            });

            return ecdsa.VerifyData(Encoding.UTF8.GetBytes(baseString), sigBytes,
                HashAlgorithmName.SHA512, DSASignatureFormat.Rfc3279DerSequence);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
