// <copyright file="SigningHttpMessageHandler.cs" company="Upvest">
// UpvestInvestmentApi.Standard
// </copyright>
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace UpvestInvestmentApi.Standard.Http.Signature
{
    /// <summary>
    /// Adds an Upvest HTTP message signature to every request passing through it.
    /// </summary>
    /// <remarks>
    /// Signing happens here, at the bottom of the handler chain, so the signature always
    /// covers the final URL, the final headers (the <c>Authorization</c> header included)
    /// and the exact body bytes that go on the wire.
    /// </remarks>
    internal sealed class SigningHttpMessageHandler : DelegatingHandler
    {
        /// <summary>Set to "1" to dump every signed request to stderr.</summary>
        private const string DebugVariable = "UPVEST_SIGNATURE_DEBUG";

        private readonly UpvestHttpMessageSigner signer;

        /// <summary>
        /// Initializes a new instance of the <see cref="SigningHttpMessageHandler"/> class.
        /// </summary>
        /// <param name="signer">The signer to delegate to.</param>
        /// <param name="innerHandler">The next handler in the chain.</param>
        public SigningHttpMessageHandler(UpvestHttpMessageSigner signer, HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
            this.signer = signer ?? throw new ArgumentNullException(nameof(signer));
        }

        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            NormaliseContentType(request);

            byte[] body = null;
            if (request.Content != null)
            {
                body = await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }

            var headers = new List<KeyValuePair<string, string>>();
            foreach (var header in request.Headers)
            {
                headers.Add(new KeyValuePair<string, string>(
                    header.Key, string.Join(", ", header.Value)));
            }

            if (request.Content != null)
            {
                foreach (var header in request.Content.Headers)
                {
                    headers.Add(new KeyValuePair<string, string>(
                        header.Key, string.Join(", ", header.Value)));
                }
            }

            List<KeyValuePair<string, string>> signedHeaders = signer.Sign(
                request.Method.Method, request.RequestUri, headers, body);

            foreach (KeyValuePair<string, string> header in signedHeaders)
            {
                Apply(request, header.Key, header.Value);
            }

            DumpWhenDebugging(request);

            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Drops the charset parameter from a JSON content type.
        /// </summary>
        /// <remarks>
        /// The framework's <see cref="StringContent"/> sends
        /// <c>application/json; charset=utf-8</c>, which the Upvest Investment API rejects
        /// with "unexpected request Content-Type". It insists on a bare
        /// <c>application/json</c>. Normalising before signing keeps the signed value and
        /// the value on the wire identical.
        /// </remarks>
        /// <param name="request">The request.</param>
        private static void NormaliseContentType(HttpRequestMessage request)
        {
            var contentType = request.Content?.Headers?.ContentType;
            if (contentType == null || string.IsNullOrEmpty(contentType.CharSet))
            {
                return;
            }

            if (string.Equals(contentType.MediaType, "application/json",
                    StringComparison.OrdinalIgnoreCase))
            {
                contentType.CharSet = null;
            }
        }

        /// <summary>
        /// Writes the outgoing request to stderr when the debug variable is set.
        /// </summary>
        /// <param name="request">The request.</param>
        private static void DumpWhenDebugging(HttpRequestMessage request)
        {
            if (System.Environment.GetEnvironmentVariable(DebugVariable) != "1")
            {
                return;
            }

            Console.Error.WriteLine($"[sig] {request.Method} {request.RequestUri}");
            foreach (var header in request.Headers)
            {
                Console.Error.WriteLine($"[sig]   {header.Key}: {string.Join(", ", header.Value)}");
            }

            if (request.Content != null)
            {
                foreach (var header in request.Content.Headers)
                {
                    Console.Error.WriteLine($"[sig]   {header.Key}: {string.Join(", ", header.Value)}");
                }
            }
        }

        /// <summary>
        /// Sets a header on the request, routing content headers to the content where the
        /// framework insists on it.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="name">The header name.</param>
        /// <param name="value">The header value.</param>
        private static void Apply(HttpRequestMessage request, string name, string value)
        {
            // Content-Length is owned by the content and is rejected on the request headers.
            if (string.Equals(name, "content-length", StringComparison.OrdinalIgnoreCase))
            {
                if (request.Content != null)
                {
                    long parsed;
                    if (long.TryParse(value, NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out parsed))
                    {
                        request.Content.Headers.ContentLength = parsed;
                    }
                }

                return;
            }

            request.Headers.Remove(name);
            if (request.Headers.TryAddWithoutValidation(name, value))
            {
                return;
            }

            // Anything the request headers reject is a content header.
            if (request.Content != null)
            {
                request.Content.Headers.Remove(name);
                request.Content.Headers.TryAddWithoutValidation(name, value);
            }
        }
    }
}
