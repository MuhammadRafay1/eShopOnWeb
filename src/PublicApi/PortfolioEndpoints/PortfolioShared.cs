using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Helpers shared by the portfolio endpoints: caller identity, status strings, and problem results.</summary>
internal static class PortfolioShared
{
    /// <summary>
    /// The calling shopper's stable identity (the JWT name claim), used as the per-shopper key. Returns null
    /// when the token carries no name — the caller is then treated as unauthenticated.
    /// </summary>
    public static string? GetBuyerId(ClaimsPrincipal user) =>
        user.Identity?.Name is { Length: > 0 } name ? name : user.FindFirstValue(ClaimTypes.Name);

    public static string ToWire(InvestorStatus status) => status.ToString().ToLowerInvariant();

    public static string ToWire(PaymentStatus status) => status.ToString().ToLowerInvariant();

    public static string ToWire(RealignmentStatus status) => status.ToString().ToLowerInvariant();

    /// <summary>Maps a gateway failure to a caller-safe HTTP result, never leaking provider detail or PII.</summary>
    public static IResult ToProblem(UpvestGatewayException ex)
    {
        // Our own credential/scope problem or quota — the caller did nothing wrong and cannot fix it.
        if (ex.IsAuthorizationScopeIssue || ex.StatusCode is 401 or 403 or 429)
        {
            return Results.Problem(
                title: "The investment provider is currently unavailable.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        // The provider rejected the caller's request — hand back the same class of status so they can act.
        if (ex.StatusCode is int s and >= 400 and < 500)
        {
            return Results.Problem(title: ex.Message, statusCode: s);
        }

        // Transport, timeout, provider 5xx, unknown.
        return Results.Problem(
            title: "The investment provider could not complete the request.",
            statusCode: StatusCodes.Status502BadGateway);
    }

    /// <summary>
    /// Reads the investor's status, degrading to <see cref="InvestorStatus.Pending"/> when the environment's
    /// Upvest client lacks the read scope (a token-level 401). Genuine provider failures still propagate so the
    /// caller can surface a 502.
    /// </summary>
    public static async System.Threading.Tasks.Task<InvestorStatus> ResolveInvestorStatusAsync(
        Microsoft.eShopWeb.ApplicationCore.Interfaces.IUpvestPortfolioGateway gateway,
        Guid userId, Guid? accountId, System.Threading.CancellationToken ct)
    {
        try
        {
            return await gateway.GetInvestorStatusAsync(userId, accountId, ct);
        }
        catch (UpvestGatewayException ex) when (ex.IsAuthorizationScopeIssue)
        {
            return InvestorStatus.Pending;
        }
    }

    public static bool TryParseAmount(decimal amount, out decimal normalized)
    {
        normalized = Math.Round(amount, 2, MidpointRounding.ToZero);
        return amount > 0m && normalized == amount; // positive and at most two decimal places
    }
}

/// <summary>
/// Serializes a decimal as a JSON number with exactly two decimal places (euros). Writing the raw value keeps
/// it a JSON number, not a string, while preserving the trailing zeros the contract requires.
/// </summary>
public sealed class MoneyJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDecimal();

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteRawValue(value.ToString("F2", CultureInfo.InvariantCulture));
}

// ------------------------------------------------------------------ request / response DTOs

public sealed class RegisterInvestorRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string BirthDate { get; set; } = string.Empty;   // ISO-8601 date (YYYY-MM-DD)
    public string BirthCity { get; set; } = string.Empty;
    public string BirthCountry { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty; // ISO 3166-1 alpha-2
    public AddressDto Address { get; set; } = new();
    public string PhoneNumber { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string TaxCountry { get; set; } = string.Empty;
    public IdentityDocumentDto IdentityDocument { get; set; } = new();
}

public sealed class AddressDto
{
    public string Line1 { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public sealed class IdentityDocumentDto
{
    public string Type { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string IssuingCountry { get; set; } = string.Empty;
    public string ExpiryDate { get; set; } = string.Empty;  // ISO-8601 date
}

public sealed class InvestorResponse
{
    public string InvestorId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class ModelFundDto
{
    public string InstrumentId { get; set; } = string.Empty;
    public decimal Weight { get; set; }
}

public sealed class UpsertModelRequest
{
    public string Name { get; set; } = string.Empty;
    public List<ModelFundDto> Funds { get; set; } = new();
}

public sealed class ModelResponse
{
    public string ModelId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<ModelFundDto> Funds { get; set; } = new();
}

public sealed class ModelListResponse
{
    public List<ModelResponse> Models { get; set; } = new();
}

public sealed class ChooseModelRequest
{
    public string ModelId { get; set; } = string.Empty;
}

public sealed class ChooseModelResponse
{
    public string ModelId { get; set; } = string.Empty;
}

public sealed class CreatePaymentRequest
{
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Amount { get; set; }
}

public sealed class PaymentResponse
{
    public string PaymentId { get; set; } = string.Empty;

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;
}

public sealed class PaymentListResponse
{
    public List<PaymentResponse> Payments { get; set; } = new();
}

public sealed class RealignmentResponse
{
    public string RealignmentId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class HoldingDto
{
    public string InstrumentId { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Value { get; set; }
}

public sealed class PortfolioResponse
{
    public string? ModelId { get; set; }
    public List<HoldingDto> Holdings { get; set; } = new();

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal TotalValue { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal PaidIn { get; set; }
}
