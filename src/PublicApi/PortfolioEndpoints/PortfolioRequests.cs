using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

// ----- Flow 1: investor sign-up -----

public class AddressDto
{
    public string? Line1 { get; set; }
    public string? Postcode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
}

public class IdentityDocumentDto
{
    public string? Type { get; set; }
    public string? Number { get; set; }
    public string? IssuingCountry { get; set; }
    public string? ExpiryDate { get; set; }
}

public class SignUpInvestorRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? BirthDate { get; set; }
    public string? BirthCity { get; set; }
    public string? BirthCountry { get; set; }
    public string? Nationality { get; set; }
    public AddressDto? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? TaxId { get; set; }
    public string? TaxCountry { get; set; }
    public IdentityDocumentDto? IdentityDocument { get; set; }

    /// <summary>Validates the form and maps it to the domain sign-up contract.</summary>
    public InvestorSignUp ToDomain()
    {
        Require(FirstName, nameof(FirstName));
        Require(LastName, nameof(LastName));
        Require(Email, nameof(Email));
        Require(Nationality, nameof(Nationality));
        Require(TaxId, nameof(TaxId));
        Require(TaxCountry, nameof(TaxCountry));

        if (Address is null)
        {
            throw PortfolioException.BadRequest("'address' is required.");
        }
        Require(Address.Line1, "address.line1");
        Require(Address.Postcode, "address.postcode");
        Require(Address.City, "address.city");
        Require(Address.Country, "address.country");

        if (IdentityDocument is null)
        {
            throw PortfolioException.BadRequest("'identityDocument' is required.");
        }
        Require(IdentityDocument.Type, "identityDocument.type");
        Require(IdentityDocument.Number, "identityDocument.number");
        Require(IdentityDocument.IssuingCountry, "identityDocument.issuingCountry");

        return new InvestorSignUp(
            FirstName!, LastName!, Email!,
            ParseDate(BirthDate, nameof(BirthDate)),
            RequireValue(BirthCity, nameof(BirthCity)),
            RequireValue(BirthCountry, nameof(BirthCountry)),
            Nationality!,
            new AddressInput(Address.Line1!, Address.Postcode!, Address.City!, Address.Country!),
            PhoneNumber ?? string.Empty,
            TaxId!, TaxCountry!,
            new IdentityDocumentInput(
                IdentityDocument.Type!, IdentityDocument.Number!, IdentityDocument.IssuingCountry!,
                ParseDate(IdentityDocument.ExpiryDate, "identityDocument.expiryDate")));
    }

    private static void Require(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw PortfolioException.BadRequest($"'{field}' is required.");
        }
    }

    private static string RequireValue(string? value, string field)
    {
        Require(value, field);
        return value!;
    }

    private static DateOnly ParseDate(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw PortfolioException.BadRequest($"'{field}' must be an ISO-8601 date (yyyy-MM-dd).");
        }
        return date;
    }
}

// ----- Flow 2: model portfolios -----

public class FundDto
{
    public string? InstrumentId { get; set; }
    public decimal Weight { get; set; }
}

public class UpsertModelRequest
{
    public string? Name { get; set; }
    public List<FundDto>? Funds { get; set; }

    public (string Name, IReadOnlyList<FundWeightInput> Funds) ToDomain()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw PortfolioException.BadRequest("'name' is required.");
        }
        if (Funds is null || Funds.Count == 0)
        {
            throw PortfolioException.BadRequest("'funds' must contain at least one fund.");
        }

        var funds = Funds.Select(f =>
        {
            if (string.IsNullOrWhiteSpace(f.InstrumentId))
            {
                throw PortfolioException.BadRequest("Each fund requires an 'instrumentId' (ISIN).");
            }
            return new FundWeightInput(f.InstrumentId!, f.Weight);
        }).ToList();

        return (Name!, funds);
    }
}

public class ChooseModelRequest
{
    public int ModelId { get; set; }
}

// ----- Flow 3: paying in -----

public class PayInRequest
{
    public decimal Amount { get; set; }
}
