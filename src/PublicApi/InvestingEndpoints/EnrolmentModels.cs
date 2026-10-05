using System;
using System.Globalization;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>The shop's investor sign-up form, as posted to <c>POST /api/investing/enrolment</c>.</summary>
public class EnrolmentRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }

    /// <summary>ISO-8601 date (YYYY-MM-DD).</summary>
    public string? BirthDate { get; set; }

    /// <summary>ISO 3166-1 alpha-2 nationality code.</summary>
    public string? Nationality { get; set; }

    public EnrolmentAddressModel? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? TaxId { get; set; }

    /// <summary>ISO 3166-1 alpha-2 tax country code.</summary>
    public string? TaxCountry { get; set; }

    /// <summary>
    /// Validate and convert to the domain form. Returns null with an <paramref name="error"/> when invalid.
    /// </summary>
    public EnrolmentForm? ToForm(out string? error)
    {
        error = null;

        if (IsBlank(FirstName)) return Fail("firstName is required", out error);
        if (IsBlank(LastName)) return Fail("lastName is required", out error);
        if (IsBlank(Email)) return Fail("email is required", out error);
        if (IsBlank(Nationality)) return Fail("nationality is required", out error);
        if (IsBlank(TaxId)) return Fail("taxId is required", out error);
        if (IsBlank(TaxCountry)) return Fail("taxCountry is required", out error);
        if (Address is null) return Fail("address is required", out error);
        if (IsBlank(Address.Line1)) return Fail("address.line1 is required", out error);
        if (IsBlank(Address.Postcode)) return Fail("address.postcode is required", out error);
        if (IsBlank(Address.City)) return Fail("address.city is required", out error);
        if (IsBlank(Address.Country)) return Fail("address.country is required", out error);

        if (!DateOnly.TryParse(BirthDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthDate))
            return Fail("birthDate must be an ISO-8601 date (YYYY-MM-DD)", out error);

        return new EnrolmentForm(
            FirstName!.Trim(),
            LastName!.Trim(),
            Email!.Trim(),
            birthDate,
            Nationality!.Trim(),
            new EnrolmentAddress(Address.Line1!.Trim(), Address.Postcode!.Trim(), Address.City!.Trim(), Address.Country!.Trim()),
            PhoneNumber?.Trim() ?? string.Empty,
            TaxId!.Trim(),
            TaxCountry!.Trim());
    }

    private static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);

    private static EnrolmentForm? Fail(string message, out string? error)
    {
        error = message;
        return null;
    }
}

public class EnrolmentAddressModel
{
    public string? Line1 { get; set; }
    public string? Postcode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
}

/// <summary>Response for the enrolment endpoints.</summary>
public class EnrolmentResponse
{
    public int EnrolmentId { get; set; }
    public string Status { get; set; } = "pending";
}
