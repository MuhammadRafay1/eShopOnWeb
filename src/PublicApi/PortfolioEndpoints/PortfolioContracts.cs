using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

// ─── Requests ───────────────────────────────────────────────────────────────────────────────────

public record InvestorSignupRequest(
    string FirstName,
    string LastName,
    string Email,
    DateTimeOffset BirthDate,
    string? BirthCity,
    string? BirthCountry,
    string Nationality,
    AddressRequest Address,
    string? PhoneNumber,
    string TaxId,
    string TaxCountry,
    IdentityDocumentRequest IdentityDocument);

public record AddressRequest(string Line1, string Postcode, string City, string Country);

public record IdentityDocumentRequest(string Type, string Number, string IssuingCountry, DateTimeOffset ExpiryDate);

public record UpsertModelRequest(string Name, List<ModelFundRequest> Funds);

public record ModelFundRequest(string InstrumentId, decimal Weight);

public record SelectModelRequest(string ModelId);

public record PayInRequest(decimal Amount);

// ─── Responses (the field names below are fixed by the task) ─────────────────────────────────────

public record InvestorResponse(Guid? InvestorId, string Status);

public record ModelResponse(string ModelId, string Name, IReadOnlyList<FundResponse> Funds);

public record FundResponse(string InstrumentId, decimal Weight);

public record PaymentResponse(Guid PaymentId, decimal Amount, string Status);

public record RealignmentResponse(Guid RealignmentId, string Status);

public record HoldingResponse(string InstrumentId, decimal Quantity, decimal Value);

public record PortfolioResponse(
    string? ModelId,
    IReadOnlyList<HoldingResponse> Holdings,
    decimal TotalValue,
    decimal PaidIn);

// ─── Helpers ──────────────────────────────────────────────────────────────────────────────────

internal static class PortfolioUser
{
    /// <summary>The signed-in shopper's user name, taken from the JWT name claim.</summary>
    public static string Name(ClaimsPrincipal user) =>
        user.Identity?.Name
        ?? user.FindFirstValue(ClaimTypes.Name)
        ?? throw new InvalidOperationException("The authenticated caller has no name claim.");
}
