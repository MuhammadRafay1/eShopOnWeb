// <copyright file="HttpSignatureCredentials.cs" company="Upvest">
// UpvestInvestmentApi.Standard
// </copyright>
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace UpvestInvestmentApi.Standard.Http.Signature
{
    /// <summary>
    /// The private key material used to sign requests to the Upvest Investment API.
    /// </summary>
    /// <remarks>
    /// Every request to the Upvest Investment API has to carry a cryptographic signature.
    /// Hand one of these to <c>UpvestInvestmentApiClient.Builder.HttpSignature(...)</c> and
    /// the SDK signs every outgoing request, including the OAuth token request it makes on
    /// your behalf.
    /// <para>
    /// See https://docs.upvest.co/products/tol/getting_started/http_signatures.
    /// </para>
    /// </remarks>
    public sealed class HttpSignatureCredentials : IDisposable
    {
        private readonly ECDsa privateKey;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpSignatureCredentials"/> class
        /// from PEM text.
        /// </summary>
        /// <param name="keyId">The key ID the matching public key is registered under.</param>
        /// <param name="privateKeyPem">The private key in PEM format.</param>
        /// <param name="privateKeyPassphrase">The passphrase, if the key is encrypted.</param>
        /// <param name="clientId">
        /// The value of the <c>upvest-client-id</c> header. When null, the client falls back
        /// to the configured OAuth client ID.
        /// </param>
        public HttpSignatureCredentials(
            string keyId,
            string privateKeyPem,
            string privateKeyPassphrase = null,
            string clientId = null)
        {
            if (string.IsNullOrEmpty(keyId))
            {
                throw new ArgumentException("A key ID is required.", nameof(keyId));
            }

            if (string.IsNullOrWhiteSpace(privateKeyPem))
            {
                throw new ArgumentException("A private key is required.", nameof(privateKeyPem));
            }

            KeyId = keyId;
            ClientId = clientId;
            privateKey = PemKeyReader.ReadEcPrivateKey(privateKeyPem, privateKeyPassphrase);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpSignatureCredentials"/> class
        /// from an already-loaded key.
        /// </summary>
        /// <param name="keyId">The key ID the matching public key is registered under.</param>
        /// <param name="privateKey">The EC private key. This instance takes ownership of it.</param>
        /// <param name="clientId">The value of the <c>upvest-client-id</c> header.</param>
        public HttpSignatureCredentials(string keyId, ECDsa privateKey, string clientId = null)
        {
            if (string.IsNullOrEmpty(keyId))
            {
                throw new ArgumentException("A key ID is required.", nameof(keyId));
            }

            KeyId = keyId;
            ClientId = clientId;
            this.privateKey = privateKey ?? throw new ArgumentNullException(nameof(privateKey));
        }

        /// <summary>
        /// Gets the key ID sent as the <c>keyid</c> signature parameter.
        /// </summary>
        public string KeyId { get; }

        /// <summary>
        /// Gets the value of the <c>upvest-client-id</c> header, if one was configured.
        /// </summary>
        public string ClientId { get; }

        /// <summary>
        /// Loads the key from a PEM file on disk.
        /// </summary>
        /// <param name="keyId">The key ID the matching public key is registered under.</param>
        /// <param name="privateKeyPath">Path to the PEM file.</param>
        /// <param name="privateKeyPassphrase">The passphrase, if the key is encrypted.</param>
        /// <param name="clientId">The value of the <c>upvest-client-id</c> header.</param>
        /// <returns>The credentials.</returns>
        public static HttpSignatureCredentials FromFile(
            string keyId,
            string privateKeyPath,
            string privateKeyPassphrase = null,
            string clientId = null)
        {
            if (string.IsNullOrEmpty(privateKeyPath))
            {
                throw new ArgumentException("A private key path is required.", nameof(privateKeyPath));
            }

            return new HttpSignatureCredentials(
                keyId, File.ReadAllText(privateKeyPath), privateKeyPassphrase, clientId);
        }

        /// <summary>
        /// Builds credentials from environment variables, or returns null when the required
        /// ones are not set.
        /// </summary>
        /// <remarks>
        /// Reads <c>UPVEST_API_KEY_ID</c> plus exactly one of
        /// <c>UPVEST_API_HTTP_SIGN_PRIVATE_KEY_FILENAME</c>,
        /// <c>UPVEST_API_HTTP_SIGN_PRIVATE_KEY</c> or
        /// <c>UPVEST_API_HTTP_SIGN_PRIVATE_KEY_BASE64</c>. Optionally reads
        /// <c>UPVEST_API_HTTP_SIGN_PRIVATE_KEY_PASSPHRASE</c> and <c>UPVEST_API_CLIENT_ID</c>.
        /// </remarks>
        /// <returns>The credentials, or null.</returns>
        public static HttpSignatureCredentials FromEnvironment()
        {
            string keyId = Read("UPVEST_API_KEY_ID");
            string pem = ReadPrivateKeyPem();
            if (string.IsNullOrEmpty(keyId) || string.IsNullOrEmpty(pem))
            {
                return null;
            }

            return new HttpSignatureCredentials(
                keyId,
                pem,
                Read("UPVEST_API_HTTP_SIGN_PRIVATE_KEY_PASSPHRASE"),
                Read("UPVEST_API_CLIENT_ID"));
        }

        /// <summary>
        /// Creates the signer which drives request signing.
        /// </summary>
        /// <param name="fallbackClientId">
        /// The client ID to use when none was configured here, normally the OAuth client ID.
        /// </param>
        /// <returns>The signer.</returns>
        internal UpvestHttpMessageSigner CreateSigner(string fallbackClientId)
        {
            var inner = new HttpMessageSigner(privateKey, KeyId);
            return new UpvestHttpMessageSigner(
                inner, string.IsNullOrEmpty(ClientId) ? fallbackClientId : ClientId);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            privateKey?.Dispose();
        }

        /// <summary>
        /// Reads the private key from the environment, in order of precedence.
        /// </summary>
        /// <returns>The PEM text, or null.</returns>
        private static string ReadPrivateKeyPem()
        {
            string path = Read("UPVEST_API_HTTP_SIGN_PRIVATE_KEY_FILENAME");
            if (!string.IsNullOrEmpty(path))
            {
                return File.ReadAllText(path);
            }

            string pem = Read("UPVEST_API_HTTP_SIGN_PRIVATE_KEY");
            if (!string.IsNullOrEmpty(pem))
            {
                return pem;
            }

            string base64 = Read("UPVEST_API_HTTP_SIGN_PRIVATE_KEY_BASE64");
            if (!string.IsNullOrEmpty(base64))
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            }

            return null;
        }

        /// <summary>
        /// Reads an environment variable, treating blanks as unset.
        /// </summary>
        /// <param name="name">The variable name.</param>
        /// <returns>The value, or null.</returns>
        private static string Read(string name)
        {
            string value = System.Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
