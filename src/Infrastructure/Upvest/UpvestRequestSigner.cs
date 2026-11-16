using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Signs outgoing Upvest requests with an HTTP Message Signature (draft-06, the version the
/// Investment API verifies): adds the <c>digest</c> (SHA-256), <c>signature-input</c> and
/// <c>signature</c> headers computed over the request's components, using ECDSA P-521 / SHA-512
/// (DER signature, base64). The loaded private key stays in memory only; nothing about it is logged.
/// </summary>
public interface IUpvestRequestSigner
{
    Task SignAsync(HttpRequestMessage request, CancellationToken ct = default);
}

public sealed class UpvestRequestSigner : IUpvestRequestSigner
{
    // Canonical component order; only components actually present on the request are included.
    private static readonly string[] ComponentOrder =
    {
        "@method", "@path", "@query", "accept", "authorization",
        "content-length", "content-type", "digest", "idempotency-key", "upvest-client-id"
    };

    private readonly string _clientId;
    private readonly string _keyId;
    private readonly ECPrivateKeyParameters _privateKey;

    public UpvestRequestSigner(IOptions<UpvestSettings> options)
    {
        var settings = options.Value;
        _clientId = settings.ClientId;
        _keyId = settings.SigningKeyId;
        _privateKey = LoadPrivateKey(settings.SigningKeyPath, settings.SigningKeyPassphrase);
    }

    public async Task SignAsync(HttpRequestMessage request, CancellationToken ct = default)
    {
        // Headers that are always present and part of the signature.
        if (!request.Headers.Contains("upvest-client-id"))
        {
            request.Headers.TryAddWithoutValidation("upvest-client-id", _clientId);
        }
        if (!request.Headers.Accept.Any())
        {
            request.Headers.Accept.ParseAdd("application/json");
        }

        // Content components (only for requests that carry a body).
        byte[]? body = null;
        if (request.Content is not null)
        {
            body = await request.Content.ReadAsByteArrayAsync(ct);
            request.Content.Headers.ContentLength = body.Length;
            if (!request.Content.Headers.Contains("digest"))
            {
                var digest = Convert.ToBase64String(SHA256.HashData(body));
                request.Content.Headers.TryAddWithoutValidation("digest", $"SHA-256={digest}");
            }
        }

        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expires = created + 300;
        var nonce = GenerateNonce();

        var components = CollectComponents(request, hasBody: body is not null);

        var signatureParams =
            "(" + string.Join(" ", components.Select(c => $"\"{c.Key}\"")) + ")" +
            $";keyid=\"{_keyId}\";created={created};expires={expires};nonce=\"{nonce}\"";

        // draft-06 signature base: unquoted component keys, one per line.
        var baseStringBuilder = new StringBuilder();
        foreach (var (key, value) in components)
        {
            baseStringBuilder.Append(key).Append(": ").Append(value).Append('\n');
        }
        baseStringBuilder.Append("@signature-params: ").Append(signatureParams);

        var signature = SignBase(baseStringBuilder.ToString());

        request.Headers.TryAddWithoutValidation("signature-input", $"sig1={signatureParams}");
        request.Headers.TryAddWithoutValidation("signature", $"sig1=:{signature}:");
    }

    private List<KeyValuePair<string, string>> CollectComponents(HttpRequestMessage request, bool hasBody)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["@method"] = request.Method.Method.ToUpperInvariant(),
            ["@path"] = request.RequestUri!.AbsolutePath,
            ["@query"] = string.IsNullOrEmpty(request.RequestUri.Query) ? null : request.RequestUri.Query,
            ["accept"] = request.Headers.Accept.Any() ? string.Join(", ", request.Headers.Accept) : null,
            ["authorization"] = request.Headers.Authorization?.ToString(),
            ["content-length"] = hasBody ? request.Content!.Headers.ContentLength?.ToString() : null,
            ["content-type"] = hasBody ? request.Content!.Headers.ContentType?.ToString() : null,
            ["digest"] = hasBody ? Single(request.Content!.Headers, "digest") : null,
            ["idempotency-key"] = Single(request.Headers, "idempotency-key"),
            ["upvest-client-id"] = Single(request.Headers, "upvest-client-id"),
        };

        var ordered = new List<KeyValuePair<string, string>>();
        foreach (var key in ComponentOrder)
        {
            if (values.TryGetValue(key, out var value) && value is not null)
            {
                ordered.Add(new KeyValuePair<string, string>(key, value));
            }
        }
        return ordered;
    }

    private static string? Single(System.Net.Http.Headers.HttpHeaders headers, string name) =>
        headers.TryGetValues(name, out var vals) ? vals.FirstOrDefault() : null;

    private string SignBase(string signatureBase)
    {
        var signer = SignerUtilities.GetSigner("SHA-512withECDSA");
        signer.Init(forSigning: true, _privateKey);
        var bytes = Encoding.UTF8.GetBytes(signatureBase);
        signer.BlockUpdate(bytes, 0, bytes.Length);
        return Convert.ToBase64String(signer.GenerateSignature());
    }

    private static string GenerateNonce()
    {
        Span<byte> buffer = stackalloc byte[16];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static ECPrivateKeyParameters LoadPrivateKey(string path, string passphrase)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new FileNotFoundException("Upvest signing key not found. Check the Upvest:SigningKeyPath setting.", path);
        }

        var pem = File.ReadAllText(path);
        using var reader = new StringReader(pem);
        var pemReader = new PemReader(reader, new PassphraseFinder(passphrase));
        var obj = pemReader.ReadObject();

        var key = obj switch
        {
            AsymmetricCipherKeyPair pair => pair.Private,
            ECPrivateKeyParameters direct => direct,
            _ => null
        };

        if (key is not ECPrivateKeyParameters ecKey)
        {
            throw new InvalidOperationException("The Upvest signing key is not a usable EC private key.");
        }

        return ecKey;
    }

    private sealed class PassphraseFinder : IPasswordFinder
    {
        private readonly char[] _passphrase;
        public PassphraseFinder(string passphrase) => _passphrase = (passphrase ?? string.Empty).ToCharArray();
        public char[] GetPassword() => (char[])_passphrase.Clone();
    }
}
