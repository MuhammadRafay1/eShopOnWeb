using System;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// The shop's investor sign-up form, carried on the enrolment request. These are the shopper's
/// personal details; they are handed to Upvest to onboard the shopper as an investor and are never
/// written to logs.
/// </summary>
public class InvestorSignUpForm
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>ISO-8601 date (date only).</summary>
    public DateTime BirthDate { get; set; }

    /// <summary>ISO 3166-1 alpha-2 nationality, e.g. "DE".</summary>
    public string Nationality { get; set; } = string.Empty;

    public InvestorAddress Address { get; set; } = new();

    public string? PhoneNumber { get; set; }

    /// <summary>Tax identification number.</summary>
    public string? TaxId { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country of tax residency, e.g. "DE".</summary>
    public string TaxCountry { get; set; } = string.Empty;
}

/// <summary>A postal address on the investor sign-up form.</summary>
public class InvestorAddress
{
    public string Line1 { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 country, e.g. "DE".</summary>
    public string Country { get; set; } = string.Empty;
}
