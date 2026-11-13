using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Low-level HTTP gateway to Upvest: acquires scoped OAuth2 tokens, signs every request (including the
/// token request), retries transient failures and re-authenticates on an expired token. Never logs
/// request bodies, tokens or secrets.
/// </summary>
public sealed class UpvestApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly UpvestRequestSigner _signer;
    private readonly UpvestTokenCache _tokenCache;
    private readonly UpvestSettings _settings;
    private readonly IAppLogger<UpvestApiClient> _logger;

    public UpvestApiClient(
        HttpClient http,
        UpvestRequestSigner signer,
        UpvestTokenCache tokenCache,
        IOptions<UpvestSettings> settings,
        IAppLogger<UpvestApiClient> logger)
    {
        _http = http;
        _signer = signer;
        _tokenCache = tokenCache;
        _settings = settings.Value;
        _logger = logger;
        if (_http.BaseAddress is null && !string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            _http.BaseAddress = new Uri(_settings.BaseUrl);
        }
    }

    public Task<UpvestResult> GetAsync(string path, string scope, CancellationToken ct) =>
        SendAsync(HttpMethod.Get, path, scope, body: null, idempotent: false, ct);

    public Task<UpvestResult> PostAsync(string path, string scope, object body, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, path, scope, body, idempotent: true, ct);

    public Task<UpvestResult> PutAsync(string path, string scope, object body, CancellationToken ct) =>
        SendAsync(HttpMethod.Put, path, scope, body, idempotent: true, ct);

    public Task<UpvestResult> PatchAsync(string path, string scope, object body, CancellationToken ct) =>
        SendAsync(HttpMethod.Patch, path, scope, body, idempotent: true, ct);

    private async Task<UpvestResult> SendAsync(HttpMethod method, string path, string scope, object? body, bool idempotent, CancellationToken ct)
    {
        var (pathOnly, query) = SplitPath(path);
        byte[]? bodyBytes = body is null ? null : JsonSerializer.SerializeToUtf8Bytes(body, JsonOptions);
        var idempotencyKey = idempotent ? Guid.NewGuid().ToString() : null;

        // Transient-failure retry policy (429/500/503). Short, capped delays keep API latency reasonable.
        var attempt = 0;
        var reauthed = false;
        while (true)
        {
            attempt++;
            var token = await GetTokenAsync(scope, forceRefresh: false, ct);

            using var request = BuildRequest(method, pathOnly, query, bodyBytes, token, idempotencyKey);
            using var response = await _http.SendAsync(request, ct);
            var status = (int)response.StatusCode;
            var payload = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Upvest {Method} {Path} -> {Status}", method.Method, pathOnly, status);
                return UpvestResult.Success(status, payload);
            }

            // Expired/invalid access token on an otherwise-authorised scope: re-auth once.
            if (status == (int)HttpStatusCode.Unauthorized && !reauthed)
            {
                reauthed = true;
                _tokenCache.Invalidate(scope);
                await GetTokenAsync(scope, forceRefresh: true, ct);
                continue;
            }

            if (IsRetryable(status) && attempt <= 3)
            {
                var delay = RetryDelay(status, attempt);
                _logger.LogWarning("Upvest {Method} {Path} -> {Status}; retrying in {Delay}s (attempt {Attempt})", method.Method, pathOnly, status, delay.TotalSeconds, attempt);
                await Task.Delay(delay, ct);
                continue;
            }

            _logger.LogWarning("Upvest {Method} {Path} -> {Status} (not retried)", method.Method, pathOnly, status);
            return UpvestResult.Failure(status, payload);
        }
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string path, string? query, byte[]? body, string token, string? idempotencyKey)
    {
        var signed = _signer.Sign(method.Method, path, query, body, body is null ? null : "application/json", token, idempotencyKey);
        var request = new HttpRequestMessage(method, path + (query ?? string.Empty));
        if (body is not null)
        {
            var content = new ByteArrayContent(body);
            content.Headers.TryAddWithoutValidation("Content-Type", "application/json");
            request.Content = content;
        }
        request.Headers.TryAddWithoutValidation("accept", "application/json");
        request.Headers.TryAddWithoutValidation("authorization", $"Bearer {token}");
        request.Headers.TryAddWithoutValidation("upvest-client-id", _settings.ClientId);
        request.Headers.TryAddWithoutValidation("upvest-api-version", "1");
        if (idempotencyKey is not null) request.Headers.TryAddWithoutValidation("idempotency-key", idempotencyKey);
        if (signed.Digest is not null) request.Headers.TryAddWithoutValidation("digest", signed.Digest);
        request.Headers.TryAddWithoutValidation("signature", signed.Signature);
        request.Headers.TryAddWithoutValidation("signature-input", signed.SignatureInput);
        return request;
    }

    private async Task<string> GetTokenAsync(string scope, bool forceRefresh, CancellationToken ct)
    {
        if (!forceRefresh && _tokenCache.TryGet(scope, out var cached))
        {
            return cached;
        }

        var gate = _tokenCache.LockFor(scope);
        await gate.WaitAsync(ct);
        try
        {
            if (!forceRefresh && _tokenCache.TryGet(scope, out cached))
            {
                return cached;
            }

            var form =
                $"client_id={Uri.EscapeDataString(_settings.ClientId)}" +
                $"&client_secret={Uri.EscapeDataString(_settings.ClientSecret)}" +
                "&grant_type=client_credentials" +
                $"&scope={Uri.EscapeDataString(scope)}";
            var bodyBytes = Encoding.UTF8.GetBytes(form);
            const string contentType = "application/x-www-form-urlencoded";

            var signed = _signer.Sign("POST", "/auth/token", null, bodyBytes, contentType, bearerToken: null, idempotencyKey: null);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/token");
            var content = new ByteArrayContent(bodyBytes);
            content.Headers.TryAddWithoutValidation("Content-Type", contentType);
            request.Content = content;
            request.Headers.TryAddWithoutValidation("accept", "application/json");
            request.Headers.TryAddWithoutValidation("upvest-client-id", _settings.ClientId);
            request.Headers.TryAddWithoutValidation("upvest-api-version", "1");
            if (signed.Digest is not null) request.Headers.TryAddWithoutValidation("digest", signed.Digest);
            request.Headers.TryAddWithoutValidation("signature", signed.Signature);
            request.Headers.TryAddWithoutValidation("signature-input", signed.SignatureInput);

            using var response = await _http.SendAsync(request, ct);
            var payload = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode == 401 && payload.Contains("not authorised to use these scopes", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Upvest token request for scope {Scope} refused: client not authorised for this scope.", scope);
                    throw new UpvestScopeNotAuthorizedException(scope);
                }
                _logger.LogWarning("Upvest token request for scope {Scope} failed with {Status}.", scope, (int)response.StatusCode);
                throw new UpvestException($"Upvest token request failed with status {(int)response.StatusCode}.");
            }

            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var token = root.GetProperty("access_token").GetString()
                        ?? throw new UpvestException("Upvest token response did not contain an access token.");
            var expiresIn = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 1800;
            // Refresh a little early to avoid using a token that expires mid-flight.
            _tokenCache.Set(scope, token, DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, expiresIn - 30)));
            return token;
        }
        finally
        {
            gate.Release();
        }
    }

    private static (string Path, string? Query) SplitPath(string pathAndQuery)
    {
        var idx = pathAndQuery.IndexOf('?');
        if (idx < 0) return (pathAndQuery, null);
        return (pathAndQuery.Substring(0, idx), pathAndQuery.Substring(idx));
    }

    private static bool IsRetryable(int status) => status is 429 or 500 or 503;

    private static TimeSpan RetryDelay(int status, int attempt) => status switch
    {
        429 => TimeSpan.FromSeconds(2 * attempt),
        503 => TimeSpan.FromSeconds(3 * attempt),
        _ => TimeSpan.FromSeconds(1 * attempt)
    };
}

/// <summary>Result of an Upvest HTTP call. <see cref="Body"/> is the parsed JSON when present.</summary>
public sealed class UpvestResult : IDisposable
{
    private readonly JsonDocument? _document;

    private UpvestResult(int statusCode, bool isSuccess, JsonDocument? document, string? problemDetail)
    {
        StatusCode = statusCode;
        IsSuccess = isSuccess;
        _document = document;
        ProblemDetail = problemDetail;
    }

    public int StatusCode { get; }
    public bool IsSuccess { get; }
    public JsonElement Body => _document?.RootElement ?? default;
    public bool HasBody => _document is not null;
    public string? ProblemDetail { get; }

    public static UpvestResult Success(int status, string payload)
    {
        JsonDocument? doc = null;
        if (!string.IsNullOrWhiteSpace(payload))
        {
            try { doc = JsonDocument.Parse(payload); } catch { /* non-JSON success body */ }
        }
        return new UpvestResult(status, true, doc, null);
    }

    public static UpvestResult Failure(int status, string payload)
    {
        string? detail = null;
        if (!string.IsNullOrWhiteSpace(payload))
        {
            try
            {
                using var doc = JsonDocument.Parse(payload);
                if (doc.RootElement.TryGetProperty("detail", out var d)) detail = d.GetString();
            }
            catch { /* ignore non-JSON error body */ }
        }
        return new UpvestResult(status, false, null, detail);
    }

    public void Dispose() => _document?.Dispose();
}
