using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Verifies the HTTP message signature on inbound Upvest webhook requests, using the public keys
/// published at <c>GET /auth/verify_keys</c>. The covered-component list is read from the request's
/// own <c>signature-input</c> header (per RFC 9421) so both v6 and v15 webhooks validate.
/// </summary>
public sealed class UpvestWebhookVerifier
{
    private readonly HttpClient _http;
    private readonly ILogger<UpvestWebhookVerifier> _logger;
    private readonly SemaphoreSlim _keysGate = new(1, 1);
    private Dictionary<string, ECParameters>? _keys;

    public UpvestWebhookVerifier(HttpClient http, ILogger<UpvestWebhookVerifier> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// Returns true if the request carries a valid Upvest signature and (when present) a body digest
    /// matching the supplied body.
    /// </summary>
    public async Task<bool> VerifyAsync(
        string method,
        string path,
        string query,
        string authority,
        IReadOnlyDictionary<string, string> headers,
        byte[] body,
        CancellationToken ct)
    {
        try
        {
            if (!headers.TryGetValue("signature", out var signatureHeader) ||
                !headers.TryGetValue("signature-input", out var signatureInput))
                return false;

            var paramsValue = signatureInput[(signatureInput.IndexOf('=') + 1)..].Trim();
            var componentNames = ParseComponentList(paramsValue);
            if (componentNames.Count == 0)
                return false;

            var keyId = ExtractQuoted(paramsValue, "keyid");
            if (keyId is null)
                return false;

            // Validate body integrity against the signed digest, if a digest component is covered.
            if (componentNames.Contains("content-digest") && headers.TryGetValue("content-digest", out var cd))
            {
                var expected = "sha-512=:" + Convert.ToBase64String(SHA512.HashData(body)) + ":";
                if (!string.Equals(expected, cd.Trim(), StringComparison.Ordinal))
                    return false;
            }
            else if (componentNames.Contains("digest") && headers.TryGetValue("digest", out var d))
            {
                var expected = "SHA-256=" + Convert.ToBase64String(SHA256.HashData(body));
                if (!string.Equals(expected, d.Trim(), StringComparison.Ordinal))
                    return false;
            }

            var baseString = BuildSignatureBase(componentNames, paramsValue, method, path, query, authority, headers, body);
            var signature = ExtractSignatureValue(signatureHeader);
            if (signature is null)
                return false;

            var key = await GetKeyAsync(keyId, ct);
            if (key is null)
                return false;

            using var ecdsa = ECDsa.Create(key.Value);
            return ecdsa.VerifyData(Encoding.UTF8.GetBytes(baseString), signature, HashAlgorithmName.SHA512, DSASignatureFormat.Rfc3279DerSequence);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Upvest webhook signature verification error: {Error}", ex.Message);
            return false;
        }
    }

    private static string BuildSignatureBase(
        List<string> componentNames, string paramsValue, string method, string path, string query,
        string authority, IReadOnlyDictionary<string, string> headers, byte[] body)
    {
        var sb = new StringBuilder();
        foreach (var name in componentNames)
        {
            string value = name switch
            {
                "@method" => method.ToUpperInvariant(),
                "@path" => path,
                "@query" => query,
                "@authority" => authority,
                "content-length" => headers.TryGetValue("content-length", out var cl) ? cl : body.Length.ToString(),
                _ => headers.TryGetValue(name, out var hv) ? hv : string.Empty,
            };
            sb.Append('"').Append(name).Append("\": ").Append(value).Append('\n');
        }
        sb.Append("\"@signature-params\": ").Append(paramsValue);
        return sb.ToString();
    }

    private static List<string> ParseComponentList(string paramsValue)
    {
        var open = paramsValue.IndexOf('(');
        var close = paramsValue.IndexOf(')');
        if (open < 0 || close < 0 || close < open)
            return new List<string>();
        var inner = paramsValue.Substring(open + 1, close - open - 1);
        return Regex.Matches(inner, "\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
    }

    private static string? ExtractQuoted(string paramsValue, string key)
    {
        var m = Regex.Match(paramsValue, key + "=\"([^\"]+)\"");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static byte[]? ExtractSignatureValue(string signatureHeader)
    {
        var m = Regex.Match(signatureHeader, ":([^:]+):");
        return m.Success ? Convert.FromBase64String(m.Groups[1].Value) : null;
    }

    private async Task<ECParameters?> GetKeyAsync(string keyId, CancellationToken ct)
    {
        if (_keys is not null && _keys.TryGetValue(keyId, out var cached))
            return cached;

        await _keysGate.WaitAsync(ct);
        try
        {
            if (_keys is null || !_keys.ContainsKey(keyId))
                _keys = await FetchKeysAsync(ct);
            return _keys.TryGetValue(keyId, out var key) ? key : null;
        }
        finally { _keysGate.Release(); }
    }

    private async Task<Dictionary<string, ECParameters>> FetchKeysAsync(CancellationToken ct)
    {
        var result = new Dictionary<string, ECParameters>();
        var body = await _http.GetStringAsync("/auth/verify_keys", ct);
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("keys", out var keys) || keys.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var jwk in keys.EnumerateArray())
        {
            var kid = jwk.TryGetProperty("kid", out var k) ? k.GetString() : null;
            var xStr = jwk.TryGetProperty("x", out var x) ? x.GetString() : null;
            var yStr = jwk.TryGetProperty("y", out var y) ? y.GetString() : null;
            if (kid is null || xStr is null || yStr is null)
                continue;

            result[kid] = new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP521,
                Q = new ECPoint { X = Base64Url(xStr), Y = Base64Url(yStr) },
            };
        }
        return result;
    }

    private static byte[] Base64Url(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
        return Convert.FromBase64String(s);
    }
}
