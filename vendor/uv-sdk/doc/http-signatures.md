# HTTP Signatures

Every request to the Upvest Investment API has to carry a cryptographic signature
which proves the identity of the caller and the integrity of the payload.

The SDK creates those signatures for you. Give the client builder an
`HttpSignatureCredentials` and it signs every outgoing request — including the
OAuth 2 token request it makes on your behalf — right before the request goes on
the wire.

* Signature draft version: **15** (`upvest-signature-version: 15`)
* Algorithm: **ECDSA over SHA-512**, DER encoded then base64
* Body coverage: `content-digest` (`sha-512=:…:`) plus `content-length`

See the [Upvest documentation](https://docs.upvest.co/products/tol/getting_started/http_signatures)
for how to generate a key pair and register the public key.

## Credentials

| Name | Type | Description |
|  --- | --- | --- |
| `keyId` | `string` | The key ID your public key is registered under at Upvest. |
| `privateKeyPem` | `string` | The private key in PEM format. |
| `privateKeyPassphrase` | `string` | The passphrase protecting the private key, if there is one. |
| `clientId` | `string` | The value of the `upvest-client-id` header. Defaults to the configured OAuth client ID. |

`HttpSignatureCredentials` owns the loaded key and implements `IDisposable`, so
keep one alive for the lifetime of the client.

## Usage Example

```csharp
using UpvestInvestmentApi.Standard;
using UpvestInvestmentApi.Standard.Authentication;
using UpvestInvestmentApi.Standard.Http.Signature;

var signatureCredentials = HttpSignatureCredentials.FromFile(
    keyId: "KeyId",
    privateKeyPath: @"C:\keys\upvest-http-sign-key.pem",
    privateKeyPassphrase: "PrivateKeyPassphrase");

UpvestInvestmentApiClient client = new UpvestInvestmentApiClient.Builder()
    .ClientCredentialsAuth(
        new ClientCredentialsAuthModel.Builder("OAuthClientId", "OAuthClientSecret")
            .OauthScopes(new List<OauthScope> { OauthScope.Usersadmin })
            .Build())
    .HttpSignature(signatureCredentials)
    .Environment(Environment.Production)
    .Build();
```

The key can also be handed over as PEM text, which avoids touching the file system:

```csharp
var signatureCredentials = new HttpSignatureCredentials(
    keyId: "KeyId",
    privateKeyPem: pemText,
    privateKeyPassphrase: "PrivateKeyPassphrase");
```

Or read straight from the environment — `FromEnvironment()` returns `null` when the
required variables are unset:

| Environment variable | Description |
|  --- | --- |
| `UPVEST_API_KEY_ID` | **Required.** The key ID registered at Upvest. |
| `UPVEST_API_HTTP_SIGN_PRIVATE_KEY_FILENAME` | Path to a `*.pem` file holding the private key. |
| `UPVEST_API_HTTP_SIGN_PRIVATE_KEY` | The private key in PEM format. |
| `UPVEST_API_HTTP_SIGN_PRIVATE_KEY_BASE64` | The PEM private key, base64-encoded onto a single line. |
| `UPVEST_API_HTTP_SIGN_PRIVATE_KEY_PASSPHRASE` | The passphrase protecting the private key. |
| `UPVEST_API_CLIENT_ID` | The value of the `upvest-client-id` header. |

The three `..._PRIVATE_KEY...` variables are mutually exclusive — provide exactly
one. They are read in the order listed above.

```csharp
var signatureCredentials = HttpSignatureCredentials.FromEnvironment();
```

## Supported key formats

Upvest issues EC keys, and the signature scheme is ECDSA over SHA-512, so an RSA
key will be rejected when it is loaded. P-256, P-384 and P-521 curves are supported.

The SDK reads all three PEM shapes OpenSSL produces for EC keys:

* `BEGIN EC PRIVATE KEY` (SEC1), unencrypted.
* `BEGIN EC PRIVATE KEY` with `Proc-Type: 4,ENCRYPTED` — the legacy OpenSSL
  encryption that `openssl ec -aes256` produces. The framework cannot read this
  format on any target, so the SDK decrypts it itself.
* `BEGIN PRIVATE KEY` (PKCS#8), unencrypted.

PKCS#8 *encrypted* keys (`BEGIN ENCRYPTED PRIVATE KEY`) are not supported; convert
one with `openssl ec -in key.pem -out key-sec1.pem -aes256`.

## What gets signed

The signature covers, in this order:

1. `@method` and the `@path` / `@query` derived components of the request URL.
2. Every request header which is not on the ignore list below — in practice
   `accept`, `authorization`, `content-type`, `upvest-client-id`,
   `upvest-api-version`, `idempotency-key` and `date`.
3. `content-digest` and `content-length`, whenever the request has a body.

Headers starting with any of `cf-`, `cdn-`, `cookie`, `x-`, `priority`,
`upvest-signature`, `sec-`, `user-agent`, `accept-encoding`, `connection`, `host`,
`expect`, `te` or `transfer-encoding` are excluded, because Upvest ignores them on
the receiving end or the transport rewrites them.

Each signature carries a `keyid`, a single-use `nonce`, a `created` timestamp and
an `expires` timestamp 10 seconds later.

## Content-Type normalisation

`StringContent` sends `application/json; charset=utf-8`, which the Upvest
Investment API rejects with *"unexpected request Content-Type"*. The signing
handler strips the charset parameter from JSON requests before signing, so the
signed value and the value on the wire stay identical.

## Debugging a rejected signature

Set `UPVEST_SIGNATURE_DEBUG=1` and the handler writes every outgoing request —
including the full `signature-input` and `signature` headers — to stderr.

A rejected signature comes back as HTTP 401. The usual causes are a wrong key ID,
a key that does not match the registered public key, or a system clock more than
ten seconds out.
