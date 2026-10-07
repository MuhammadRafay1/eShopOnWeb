// <copyright file="HttpMessageSigner.cs" company="Upvest">
// UpvestInvestmentApi.Standard
// </copyright>
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace UpvestInvestmentApi.Standard.Http.Signature
{
    /// <summary>
    /// Creates HTTP message signatures, as described by version 15 of the HTTP Message
    /// Signatures standard draft.
    /// </summary>
    /// <remarks>
    /// Only the parts the Upvest Investment API relies on are implemented. The class holds
    /// no Upvest-specific rules and no knowledge of any HTTP client; see
    /// <see cref="UpvestHttpMessageSigner"/> for that layer.
    /// <para>
    /// See https://datatracker.ietf.org/doc/html/draft-ietf-httpbis-message-signatures-15.
    /// </para>
    /// </remarks>
    internal sealed class HttpMessageSigner
    {
        /// <summary>The derived URL components covered by default.</summary>
        private static readonly string[] DefaultUrlParts = { "@path", "@query" };

        /// <summary>How long a signature stays valid, counted from its creation.</summary>
        private static readonly TimeSpan DefaultExpiry = TimeSpan.FromSeconds(10);

        private readonly ECDsa privateKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpMessageSigner"/> class.
        /// </summary>
        /// <param name="privateKey">The EC private key used to sign.</param>
        /// <param name="keyId">The key ID the matching public key is registered under.</param>
        public HttpMessageSigner(ECDsa privateKey, string keyId)
        {
            if (string.IsNullOrEmpty(keyId))
            {
                throw new ArgumentException("A key ID is required.", nameof(keyId));
            }

            this.privateKey = privateKey ?? throw new ArgumentNullException(nameof(privateKey));
            KeyId = keyId;
        }

        /// <summary>
        /// Gets the key ID sent as the <c>keyid</c> signature parameter.
        /// </summary>
        public string KeyId { get; }

        /// <summary>
        /// Signs one request.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="url">The full request URL.</param>
        /// <param name="headers">The headers to cover, in the order they should be covered.</param>
        /// <param name="body">The request body, or null.</param>
        /// <param name="createdAt">The signature creation time.</param>
        /// <returns>The headers which have to be added to the request.</returns>
        public SignedRequest Sign(
            string method,
            Uri url,
            IEnumerable<KeyValuePair<string, string>> headers,
            byte[] body,
            DateTimeOffset createdAt)
        {
            var covered = new List<KeyValuePair<string, string>>();

            if (!string.IsNullOrEmpty(method))
            {
                covered.Add(Component("@method", method.ToUpperInvariant()));
            }

            IDictionary<string, string> urlParts = DeriveUrlComponents(url);
            foreach (string part in DefaultUrlParts)
            {
                string value;
                if (urlParts.TryGetValue(part, out value) && value != null)
                {
                    covered.Add(Component(part, value));
                }
            }

            // Headers already on the request, lower-cased and de-duplicated. Later values win,
            // matching how a server folds repeated headers.
            var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (headers != null)
            {
                foreach (KeyValuePair<string, string> header in headers)
                {
                    string name = header.Key.ToLowerInvariant();
                    int index;
                    if (seen.TryGetValue(name, out index))
                    {
                        covered[index] = Component(name, header.Value);
                    }
                    else
                    {
                        seen[name] = covered.Count;
                        covered.Add(Component(name, header.Value));
                    }
                }
            }

            var added = new List<KeyValuePair<string, string>>
            {
                Component("date", createdAt.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture)),
            };

            // Covering the digest and the length is what indirectly covers the body.
            if (body != null && body.Length > 0)
            {
                using (var sha512 = SHA512.Create())
                {
                    string digest = Convert.ToBase64String(sha512.ComputeHash(body));
                    added.Add(Component("content-digest", "sha-512=:" + digest + ":"));
                }

                added.Add(Component("content-length",
                    body.Length.ToString(CultureInfo.InvariantCulture)));
            }

            foreach (KeyValuePair<string, string> header in added)
            {
                int index;
                if (seen.TryGetValue(header.Key, out index))
                {
                    covered[index] = header;
                }
                else
                {
                    seen[header.Key] = covered.Count;
                    covered.Add(header);
                }
            }

            long created = createdAt.ToUnixTimeSeconds();
            long expires = createdAt.Add(DefaultExpiry).ToUnixTimeSeconds();
            string signatureParams = BuildSignatureParams(covered, created, expires);

            var signatureBase = new StringBuilder();
            foreach (KeyValuePair<string, string> component in covered)
            {
                signatureBase.Append('"').Append(component.Key).Append("\": ")
                    .Append(component.Value).Append('\n');
            }

            signatureBase.Append("\"@signature-params\": ").Append(signatureParams);

            byte[] signatureBaseBytes = Encoding.UTF8.GetBytes(signatureBase.ToString());
            string signature = Convert.ToBase64String(SignToDer(signatureBaseBytes));

            var result = new SignedRequest
            {
                SignatureBase = signatureBaseBytes,
                Headers = new List<KeyValuePair<string, string>>(added),
            };
            result.Headers.Add(Component("signature-input", "sig1=" + signatureParams));
            result.Headers.Add(Component("signature", "sig1=:" + signature + ":"));
            return result;
        }

        /// <summary>
        /// Builds the <c>@signature-params</c> value.
        /// </summary>
        /// <param name="covered">The covered components.</param>
        /// <param name="created">The creation timestamp.</param>
        /// <param name="expires">The expiry timestamp.</param>
        /// <returns>The serialised signature parameters.</returns>
        private string BuildSignatureParams(
            IList<KeyValuePair<string, string>> covered, long created, long expires)
        {
            var keys = new StringBuilder();
            for (int i = 0; i < covered.Count; i++)
            {
                if (i > 0)
                {
                    keys.Append(' ');
                }

                keys.Append('"').Append(covered[i].Key).Append('"');
            }

            // `created` and `expires` are integers, so they are not quoted.
            return "(" + keys + ")"
                + ";keyid=\"" + KeyId + "\""
                + ";nonce=\"" + NewNonce() + "\""
                + ";created=" + created.ToString(CultureInfo.InvariantCulture)
                + ";expires=" + expires.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Generates a single-use value which protects against signature replay.
        /// </summary>
        /// <returns>The nonce.</returns>
        private static string NewNonce() => Guid.NewGuid().ToString();

        /// <summary>
        /// Extracts the derived components of a request URL.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <returns>The derived components.</returns>
        private static IDictionary<string, string> DeriveUrlComponents(Uri url)
        {
            var parts = new Dictionary<string, string>(StringComparer.Ordinal);
            if (url == null)
            {
                return parts;
            }

            string scheme = url.Scheme.ToLowerInvariant();
            string authority = url.Authority.ToLowerInvariant();
            if ((scheme == "https" && url.Port == 443) || (scheme == "http" && url.Port == 80))
            {
                authority = url.Host.ToLowerInvariant();
            }

            string path = string.IsNullOrEmpty(url.AbsolutePath) ? "/" : url.AbsolutePath;

            parts["@scheme"] = scheme;
            parts["@authority"] = authority;
            parts["@path"] = path;

            // `@query` carries its leading '?' and is left out entirely when empty.
            string query = url.Query;
            parts["@query"] = string.IsNullOrEmpty(query) || query == "?" ? null : query;
            return parts;
        }

        /// <summary>
        /// Signs the data and returns the signature in the DER encoding Upvest expects.
        /// </summary>
        /// <remarks>
        /// The framework produces IEEE P1363 (<c>r || s</c>) signatures, whereas Upvest and
        /// every one of their reference implementations use the ASN.1 DER form.
        /// </remarks>
        /// <param name="data">The data to sign.</param>
        /// <returns>The DER encoded signature.</returns>
        private byte[] SignToDer(byte[] data)
        {
            byte[] raw = privateKey.SignData(data, HashAlgorithmName.SHA512);
            if (raw.Length % 2 != 0)
            {
                throw new CryptographicException("Unexpected ECDSA signature length.");
            }

            int half = raw.Length / 2;
            var r = new byte[half];
            var s = new byte[half];
            Buffer.BlockCopy(raw, 0, r, 0, half);
            Buffer.BlockCopy(raw, half, s, 0, half);

            byte[] rDer = DerInteger(r);
            byte[] sDer = DerInteger(s);

            var content = new byte[rDer.Length + sDer.Length];
            Buffer.BlockCopy(rDer, 0, content, 0, rDer.Length);
            Buffer.BlockCopy(sDer, 0, content, rDer.Length, sDer.Length);

            using (var stream = new MemoryStream())
            {
                stream.WriteByte(0x30);
                WriteDerLength(stream, content.Length);
                stream.Write(content, 0, content.Length);
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Encodes a big-endian unsigned value as a DER INTEGER.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The DER encoded integer.</returns>
        private static byte[] DerInteger(byte[] value)
        {
            int start = 0;
            while (start < value.Length - 1 && value[start] == 0)
            {
                start++;
            }

            int length = value.Length - start;

            // DER integers are signed, so a leading bit of 1 needs a 0x00 prefix.
            bool needsPad = (value[start] & 0x80) != 0;

            using (var stream = new MemoryStream())
            {
                stream.WriteByte(0x02);
                WriteDerLength(stream, needsPad ? length + 1 : length);
                if (needsPad)
                {
                    stream.WriteByte(0x00);
                }

                stream.Write(value, start, length);
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Writes a DER length prefix.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="length">The length.</param>
        private static void WriteDerLength(Stream stream, int length)
        {
            if (length < 0x80)
            {
                stream.WriteByte((byte)length);
                return;
            }

            var bytes = new List<byte>();
            int remaining = length;
            while (remaining > 0)
            {
                bytes.Insert(0, (byte)(remaining & 0xFF));
                remaining >>= 8;
            }

            stream.WriteByte((byte)(0x80 | bytes.Count));
            foreach (byte b in bytes)
            {
                stream.WriteByte(b);
            }
        }

        /// <summary>
        /// Convenience factory for a covered component.
        /// </summary>
        /// <param name="name">The component name.</param>
        /// <param name="value">The component value.</param>
        /// <returns>The component.</returns>
        private static KeyValuePair<string, string> Component(string name, string value)
            => new KeyValuePair<string, string>(name, value ?? string.Empty);
    }

    /// <summary>
    /// The outcome of signing a request.
    /// </summary>
    internal sealed class SignedRequest
    {
        /// <summary>
        /// Gets or sets the headers which have to be added to the request.
        /// </summary>
        public List<KeyValuePair<string, string>> Headers { get; set; }

        /// <summary>
        /// Gets or sets the exact bytes which were signed. Useful when comparing against
        /// another implementation.
        /// </summary>
        public byte[] SignatureBase { get; set; }
    }
}
