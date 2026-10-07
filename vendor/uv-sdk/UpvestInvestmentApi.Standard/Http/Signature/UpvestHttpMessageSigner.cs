// <copyright file="UpvestHttpMessageSigner.cs" company="Upvest">
// UpvestInvestmentApi.Standard
// </copyright>
using System;
using System.Collections.Generic;
using System.Linq;

namespace UpvestInvestmentApi.Standard.Http.Signature
{
    /// <summary>
    /// Applies the Upvest-specific rules around an HTTP message signature: which headers
    /// are covered, which Upvest headers are added, and the signature version marker.
    /// </summary>
    /// <remarks>
    /// See https://docs.upvest.co/products/tol/getting_started/http_signatures.
    /// </remarks>
    internal sealed class UpvestHttpMessageSigner
    {
        /// <summary>The signature draft version the Upvest Investment API expects.</summary>
        public const string SignatureVersion = "15";

        /// <summary>The Upvest Investment API version this SDK talks to.</summary>
        public const string ApiVersion = "1";

        /// <summary>The only values the Upvest Investment API accepts in an `accept` header.</summary>
        private static readonly string[] AcceptableAcceptValues =
        {
            "application/json",
            "application/pdf",
        };

        /// <summary>
        /// Headers starting with any of these are left out of the signature, because they
        /// are added by intermediaries or explicitly ignored by Upvest.
        /// </summary>
        private static readonly string[] IgnorableHeaderPrefixes =
        {
            "cf-",
            "cdn-",
            "cookie",
            "x-",
            "priority",
            "upvest-signature",
            "sec-",
            "user-agent",
            "accept-encoding",
            "connection",
            "host",
            "expect",
            "te",
            "transfer-encoding",
        };

        private readonly HttpMessageSigner signer;
        private readonly string clientId;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpvestHttpMessageSigner"/> class.
        /// </summary>
        /// <param name="signer">The underlying generic signer.</param>
        /// <param name="clientId">The Upvest client ID, or null to leave the header alone.</param>
        public UpvestHttpMessageSigner(HttpMessageSigner signer, string clientId)
        {
            this.signer = signer ?? throw new ArgumentNullException(nameof(signer));
            this.clientId = clientId;
        }

        /// <summary>
        /// Tells whether a header must be left out of the signature.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <returns>True when the header must not be signed.</returns>
        public static bool IsIgnorable(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return true;
            }

            string lower = name.ToLowerInvariant();
            return IgnorableHeaderPrefixes.Any(prefix =>
                lower.StartsWith(prefix, StringComparison.Ordinal));
        }

        /// <summary>
        /// Signs a request and returns every header which has to be added to it.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="url">The full request URL.</param>
        /// <param name="headers">The headers already on the request.</param>
        /// <param name="body">The request body, or null.</param>
        /// <returns>The headers to add.</returns>
        public List<KeyValuePair<string, string>> Sign(
            string method,
            Uri url,
            IEnumerable<KeyValuePair<string, string>> headers,
            byte[] body)
        {
            var existing = new List<KeyValuePair<string, string>>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (headers != null)
            {
                foreach (KeyValuePair<string, string> header in headers)
                {
                    if (IsIgnorable(header.Key))
                    {
                        continue;
                    }

                    existing.Add(new KeyValuePair<string, string>(
                        header.Key.ToLowerInvariant(), header.Value));
                    names.Add(header.Key);
                }
            }

            var upvestHeaders = new List<KeyValuePair<string, string>>();

            string accept = existing
                .Where(h => string.Equals(h.Key, "accept", StringComparison.OrdinalIgnoreCase))
                .Select(h => h.Value)
                .FirstOrDefault();
            if (accept == null || !AcceptableAcceptValues.Contains(accept))
            {
                upvestHeaders.Add(new KeyValuePair<string, string>("accept", "application/json"));
            }

            if (!names.Contains("upvest-api-version"))
            {
                upvestHeaders.Add(new KeyValuePair<string, string>("upvest-api-version", ApiVersion));
            }

            if (!string.IsNullOrEmpty(clientId) && !names.Contains("upvest-client-id"))
            {
                upvestHeaders.Add(new KeyValuePair<string, string>("upvest-client-id", clientId));
            }

            var toCover = new List<KeyValuePair<string, string>>(existing);
            foreach (KeyValuePair<string, string> header in upvestHeaders)
            {
                int index = toCover.FindIndex(h =>
                    string.Equals(h.Key, header.Key, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    toCover[index] = header;
                }
                else
                {
                    toCover.Add(header);
                }
            }

            SignedRequest signed = signer.Sign(method, url, toCover, body, DateTimeOffset.UtcNow);

            var result = new List<KeyValuePair<string, string>>(upvestHeaders);
            result.AddRange(signed.Headers);

            // The version marker is never covered by the signature, so it goes on last.
            result.Add(new KeyValuePair<string, string>(
                "upvest-signature-version", SignatureVersion));
            return result;
        }
    }
}
