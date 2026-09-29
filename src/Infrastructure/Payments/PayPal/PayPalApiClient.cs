using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Payments.PayPal;

/// <summary>
/// Thin transport wrapper around the shared PayPal HttpClient: attaches the bearer token and an
/// optional PayPal-Request-Id idempotency header, serialises/deserialises JSON with PayPal's
/// snake_case convention, and normalises PayPal error responses into a parsed shape callers can
/// branch on (e.g. the AUTHORIZATION_EXPIRED issue). Never logs request/response bodies, so card
/// details forwarded to PayPal are never written to logs.
/// </summary>
public class PayPalApiClient
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPayPalAccessTokenProvider _tokenProvider;
    private readonly ILogger<PayPalApiClient> _logger;

    public PayPalApiClient(IHttpClientFactory httpClientFactory,
        IPayPalAccessTokenProvider tokenProvider, ILogger<PayPalApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    public Task<PayPalApiResponse<TResponse>> PostAsync<TResponse>(string path, object? body,
        string? idempotencyKey, CancellationToken ct, bool preferRepresentation = false) =>
        SendAsync<TResponse>(HttpMethod.Post, path, body, idempotencyKey, ct, preferRepresentation);

    public Task<PayPalApiResponse<TResponse>> GetAsync<TResponse>(string path, CancellationToken ct) =>
        SendAsync<TResponse>(HttpMethod.Get, path, null, null, ct);

    public async Task<PayPalApiResponse<bool>> DeleteAsync(string path, CancellationToken ct)
    {
        var result = await SendRawAsync(HttpMethod.Delete, path, null, null, ct);
        return new PayPalApiResponse<bool>
        {
            StatusCode = result.StatusCode,
            IsSuccess = result.IsSuccess,
            Value = result.IsSuccess,
            Error = result.Error
        };
    }

    private async Task<PayPalApiResponse<TResponse>> SendAsync<TResponse>(HttpMethod method,
        string path, object? body, string? idempotencyKey, CancellationToken ct,
        bool preferRepresentation = false)
    {
        var raw = await SendRawAsync(method, path, body, idempotencyKey, ct, preferRepresentation);
        var response = new PayPalApiResponse<TResponse>
        {
            StatusCode = raw.StatusCode,
            IsSuccess = raw.IsSuccess,
            Error = raw.Error
        };

        if (raw.IsSuccess && !string.IsNullOrWhiteSpace(raw.Body))
        {
            try
            {
                response.Value = JsonSerializer.Deserialize<TResponse>(raw.Body, JsonOptions);
            }
            catch (Exception ex)
            {
                throw new PayPalIntegrationException(
                    $"Could not parse PayPal response for {method} {path}.", ex);
            }
        }

        return response;
    }

    private async Task<RawResult> SendRawAsync(HttpMethod method, string path, object? body,
        string? idempotencyKey, CancellationToken ct, bool preferRepresentation = false)
    {
        var client = _httpClientFactory.CreateClient(PayPalHttpClientNames.PayPal);
        var token = await _tokenProvider.GetAccessTokenAsync(ct);

        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            request.Headers.TryAddWithoutValidation("PayPal-Request-Id", idempotencyKey);
        }
        if (preferRepresentation)
        {
            // Ask PayPal to return the full resource (e.g. the capture's seller_receivable_breakdown),
            // which is omitted from the default minimal response.
            request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
        }
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new PayPalIntegrationException($"Failed to reach PayPal for {method} {path}.", ex);
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            return new RawResult { StatusCode = response.StatusCode, IsSuccess = true, Body = responseBody };
        }

        // 5xx that survived the retry policy is an integration failure, not a business decline.
        if ((int)response.StatusCode >= 500)
        {
            _logger.LogError("PayPal {Method} {Path} failed with server status {Status}",
                method, path, (int)response.StatusCode);
            throw new PayPalIntegrationException(
                $"PayPal returned server error {(int)response.StatusCode} for {method} {path}.");
        }

        var error = ParseError(responseBody, response.StatusCode);
        _logger.LogWarning("PayPal {Method} {Path} returned {Status}: {Issue}",
            method, path, (int)response.StatusCode, error.PrimaryIssue);
        return new RawResult { StatusCode = response.StatusCode, IsSuccess = false, Error = error };
    }

    private static PayPalErrorDetails ParseError(string body, HttpStatusCode status)
    {
        var details = new PayPalErrorDetails { HttpStatus = status };
        if (string.IsNullOrWhiteSpace(body))
        {
            return details;
        }

        try
        {
            var wire = JsonSerializer.Deserialize<PayPalErrorWire>(body, JsonOptions);
            if (wire is not null)
            {
                details.Name = wire.Name;
                details.Message = wire.Message;
                if (wire.Details is not null)
                {
                    details.Issues = wire.Details
                        .Select(d => new PayPalIssue(d.Issue ?? string.Empty, d.Description ?? string.Empty))
                        .ToList();
                }
            }
        }
        catch
        {
            // Non-JSON error body; keep the raw text as the message so it's still actionable.
            details.Message = body.Length > 500 ? body[..500] : body;
        }

        return details;
    }

    private sealed class RawResult
    {
        public HttpStatusCode StatusCode { get; init; }
        public bool IsSuccess { get; init; }
        public string? Body { get; init; }
        public PayPalErrorDetails? Error { get; init; }
    }

    private sealed class PayPalErrorWire
    {
        public string? Name { get; set; }
        public string? Message { get; set; }
        public List<PayPalErrorDetailWire>? Details { get; set; }
    }

    private sealed class PayPalErrorDetailWire
    {
        public string? Issue { get; set; }
        public string? Description { get; set; }
    }
}

public class PayPalApiResponse<T>
{
    public HttpStatusCode StatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public T? Value { get; set; }
    public PayPalErrorDetails? Error { get; set; }
}

public class PayPalErrorDetails
{
    public HttpStatusCode HttpStatus { get; set; }
    public string? Name { get; set; }
    public string? Message { get; set; }
    public List<PayPalIssue> Issues { get; set; } = new();

    public string? PrimaryIssue => Issues.Count > 0 ? Issues[0].Issue : Name;

    public bool HasIssue(string issue) =>
        Issues.Any(i => string.Equals(i.Issue, issue, StringComparison.OrdinalIgnoreCase));

    /// <summary>A concise, operator-actionable description built from PayPal's own text.</summary>
    public string Describe()
    {
        if (Issues.Count > 0)
        {
            return string.Join("; ", Issues.Select(i =>
                string.IsNullOrEmpty(i.Description) ? i.Issue : $"{i.Issue}: {i.Description}"));
        }
        if (!string.IsNullOrEmpty(Message))
        {
            return string.IsNullOrEmpty(Name) ? Message! : $"{Name}: {Message}";
        }
        return $"PayPal request failed with status {(int)HttpStatus}.";
    }
}

public record PayPalIssue(string Issue, string Description);
