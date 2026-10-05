using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Verifies the HTTP Message Signature on an inbound Upvest webhook against the JWKS public keys,
/// per the documented v15 scheme (ECDSA P-521 / SHA-512).
/// </summary>
public static class UpvestWebhookVerifier
{
    public static bool Verify(
        string method,
        string path,
        string query,
        Func<string, string?> headerLookup,
        IReadOnlyList<UpvestJwk> keys)
    {
        var signatureInput = headerLookup("signature-input");
        var signatureHeader = headerLookup("signature");
        if (string.IsNullOrEmpty(signatureInput) || string.IsNullOrEmpty(signatureHeader))
            return false;

        // signature-input: sig1=("c1" "c2" ...);keyid="..";created=..;expires=..;nonce=".."
        var eq = signatureInput.IndexOf('=');
        if (eq < 0) return false;
        var paramsValue = signatureInput[(eq + 1)..].Trim();

        var open = paramsValue.IndexOf('(');
        var close = paramsValue.IndexOf(')');
        if (open < 0 || close < 0 || close < open) return false;

        var componentSegment = paramsValue.Substring(open + 1, close - open - 1);
        var componentNames = new List<string>();
        foreach (var raw in componentSegment.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            componentNames.Add(raw.Trim().Trim('"'));

        var keyId = ExtractQuoted(paramsValue, "keyid");
        if (keyId is null) return false;

        // Reconstruct the signature base.
        var sb = new StringBuilder();
        foreach (var name in componentNames)
        {
            string value;
            switch (name)
            {
                case "@method": value = method.ToUpperInvariant(); break;
                case "@path": value = path; break;
                case "@query": value = query; break;
                default:
                    var headerValue = headerLookup(name);
                    if (headerValue is null) return false; // signed component missing
                    value = headerValue;
                    break;
            }
            sb.Append('"').Append(name).Append("\": ").Append(value).Append('\n');
        }
        sb.Append("\"@signature-params\": ").Append(paramsValue);

        // signature: sig1=:BASE64:
        var sigColon = signatureHeader.IndexOf(":", StringComparison.Ordinal);
        var lastColon = signatureHeader.LastIndexOf(':');
        if (sigColon < 0 || lastColon <= sigColon) return false;
        var sigBase64 = signatureHeader.Substring(sigColon + 1, lastColon - sigColon - 1);

        byte[] signature;
        try { signature = Convert.FromBase64String(sigBase64); }
        catch { return false; }

        var jwk = keys is null ? null : Find(keys, keyId);
        if (jwk is null) return false;

        try
        {
            using var ecdsa = CreateEcdsa(jwk);
            return ecdsa.VerifyData(
                Encoding.UTF8.GetBytes(sb.ToString()),
                signature,
                HashAlgorithmName.SHA512,
                DSASignatureFormat.Rfc3279DerSequence);
        }
        catch
        {
            return false;
        }
    }

    private static UpvestJwk? Find(IReadOnlyList<UpvestJwk> keys, string kid)
    {
        foreach (var k in keys)
            if (string.Equals(k.Kid, kid, StringComparison.Ordinal))
                return k;
        return null;
    }

    private static ECDsa CreateEcdsa(UpvestJwk jwk)
    {
        var parameters = new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP521,
            Q = new ECPoint
            {
                X = Base64UrlDecode(jwk.X),
                Y = Base64UrlDecode(jwk.Y)
            }
        };
        return ECDsa.Create(parameters);
    }

    private static string? ExtractQuoted(string source, string key)
    {
        var marker = key + "=\"";
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return null;
        start += marker.Length;
        var end = source.IndexOf('"', start);
        return end < 0 ? null : source[start..end];
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
