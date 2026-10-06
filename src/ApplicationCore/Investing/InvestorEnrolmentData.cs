using System;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// The shop's investor sign-up form, as supplied by an opting-in shopper. Carries personal details that
/// must never be written to logs.
/// </summary>
public sealed record InvestorEnrolmentData
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }

    /// <summary>Date of birth (the date part of an ISO-8601 date).</summary>
    public required DateTimeOffset BirthDate { get; init; }

    /// <summary>ISO 3166-1 alpha-2 nationality code.</summary>
    public required string Nationality { get; init; }

    public required EnrolmentAddress Address { get; init; }

    public string? PhoneNumber { get; init; }

    public required string TaxId { get; init; }

    /// <summary>ISO 3166-1 alpha-2 country code of tax residence.</summary>
    public required string TaxCountry { get; init; }
}

/// <summary>Postal address from the sign-up form.</summary>
public sealed record EnrolmentAddress
{
    public required string Line1 { get; init; }
    public required string Postcode { get; init; }
    public required string City { get; init; }

    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public required string Country { get; init; }
}
