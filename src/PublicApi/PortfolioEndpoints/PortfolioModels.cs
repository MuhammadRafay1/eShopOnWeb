using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

// ----- Requests -----

/// <summary>The shop's investor sign-up form (Flow 1).</summary>
public class RegisterInvestorRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string BirthDate { get; set; } = string.Empty;
    public string BirthCity { get; set; } = string.Empty;
    public string BirthCountry { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public AddressRequest Address { get; set; } = new();
    public string PhoneNumber { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string TaxCountry { get; set; } = string.Empty;
    public IdentityDocumentRequest IdentityDocument { get; set; } = new();

    /// <summary>Set from the authenticated caller — never from the request body.</summary>
    [JsonIgnore]
    public string BuyerId { get; set; } = string.Empty;
}

public class AddressRequest
{
    public string Line1 { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class IdentityDocumentRequest
{
    public string Type { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string IssuingCountry { get; set; } = string.Empty;
    public string ExpiryDate { get; set; } = string.Empty;
}

/// <summary>Create or replace a model portfolio (Flow 2).</summary>
public class UpsertModelRequest
{
    public string Name { get; set; } = string.Empty;
    public List<FundRequest> Funds { get; set; } = new();

    [JsonIgnore]
    public string ModelId { get; set; } = string.Empty;
}

public class FundRequest
{
    public string InstrumentId { get; set; } = string.Empty;
    public decimal Weight { get; set; }
}

/// <summary>The investor's choice of model (Flow 2).</summary>
public class SelectModelRequest
{
    public string ModelId { get; set; } = string.Empty;

    [JsonIgnore]
    public string BuyerId { get; set; } = string.Empty;
}

/// <summary>Pay an amount in, in euros (Flow 3).</summary>
public class PayInRequest
{
    public decimal Amount { get; set; }

    [JsonIgnore]
    public string BuyerId { get; set; } = string.Empty;
}

// ----- Responses (field names are fixed by the task) -----

public record InvestorResponse(string InvestorId, string Status);

public record FundResponse(string InstrumentId, decimal Weight);

public record ModelResponse(string ModelId, string Name, IReadOnlyList<FundResponse> Funds);

public record PaymentResponse(Guid PaymentId, decimal Amount, string Status);

public record PaymentCreatedResponse(Guid PaymentId, string Status);

public record RealignmentResponse(Guid RealignmentId, string Status);

public record HoldingResponse(string InstrumentId, decimal Quantity, decimal Value);

public record PortfolioResponse(
    string? ModelId,
    IReadOnlyList<HoldingResponse> Holdings,
    decimal TotalValue,
    decimal PaidIn);
