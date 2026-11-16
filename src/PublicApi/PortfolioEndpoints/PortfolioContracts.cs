using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Serializes a monetary <see cref="decimal"/> as a JSON number with exactly two decimal places
/// (euros), as required by the portfolio API contract.
/// </summary>
public sealed class MoneyJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.String
            ? decimal.Parse(reader.GetString()!, NumberStyles.Any, CultureInfo.InvariantCulture)
            : reader.GetDecimal();

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        => writer.WriteRawValue(value.ToString("0.00", CultureInfo.InvariantCulture));
}

// ---------------- Flow 1 — Becoming an investor ----------------

public sealed record AddressDto(string? Line1, string? Postcode, string? City, string? Country);

public sealed record IdentityDocumentDto(string? Type, string? Number, string? IssuingCountry, DateOnly ExpiryDate);

public sealed record SignUpInvestorRequest(
    string? FirstName,
    string? LastName,
    string? Email,
    DateOnly BirthDate,
    string? BirthCity,
    string? BirthCountry,
    string? Nationality,
    AddressDto? Address,
    string? PhoneNumber,
    string? TaxId,
    string? TaxCountry,
    IdentityDocumentDto? IdentityDocument);

public sealed record InvestorResponse(string InvestorId, string Status);

// ---------------- Flow 2 — Model portfolios ----------------

public sealed record ModelFundDto(string? InstrumentId, decimal Weight);

public sealed record UpsertModelRequest(string? Name, List<ModelFundDto>? Funds);

public sealed record ModelFundResponse(string InstrumentId, decimal Weight);

public sealed record ModelResponse(string ModelId, string Name, IReadOnlyList<ModelFundResponse> Funds);

public sealed record ModelListResponse(IReadOnlyList<ModelResponse> Models);

public sealed record ChooseModelRequest(string? ModelId);

// ---------------- Flow 3 — Paying in ----------------

public sealed record PayInRequest([property: JsonConverter(typeof(MoneyJsonConverter))] decimal Amount);

public sealed record PaymentResponse(
    string PaymentId,
    [property: JsonConverter(typeof(MoneyJsonConverter))] decimal Amount,
    string Status);

public sealed record PaymentListResponse(IReadOnlyList<PaymentResponse> Payments);

// ---------------- Flow 4 — Realignment ----------------

public sealed record RealignmentResponse(string RealignmentId, string Status);

// ---------------- Flow 5 — The investor's portfolio ----------------

public sealed record HoldingResponse(
    string InstrumentId,
    decimal Quantity,
    [property: JsonConverter(typeof(MoneyJsonConverter))] decimal Value);

public sealed record PortfolioResponse(
    string? ModelId,
    IReadOnlyList<HoldingResponse> Holdings,
    [property: JsonConverter(typeof(MoneyJsonConverter))] decimal TotalValue,
    [property: JsonConverter(typeof(MoneyJsonConverter))] decimal PaidIn);

/// <summary>Maps domain results to wire responses and shared status strings.</summary>
internal static class PortfolioMapping
{
    public static string Wire(this InvestorStatus s) => s.ToString().ToLowerInvariant();
    public static string Wire(this PaymentStatus s) => s.ToString().ToLowerInvariant();
    public static string Wire(this RealignmentStatus s) => s.ToString().ToLowerInvariant();

    public static InvestorResponse ToResponse(this InvestorResult r) => new(r.InvestorId, r.Status.Wire());

    public static ModelResponse ToResponse(this ModelPortfolio m) =>
        new(m.ModelId, m.Name, System.Linq.Enumerable.ToList(
            System.Linq.Enumerable.Select(m.Funds, f => new ModelFundResponse(f.InstrumentId, f.Weight))));

    public static PaymentResponse ToResponse(this PaymentResult p) => new(p.PaymentId, p.Amount, p.Status.Wire());

    public static RealignmentResponse ToResponse(this RealignmentResult r) => new(r.RealignmentId, r.Status.Wire());
}
