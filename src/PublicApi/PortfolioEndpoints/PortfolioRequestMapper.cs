using System.Collections.Generic;
using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Maps API request DTOs onto the application's domain contracts.</summary>
internal static class PortfolioRequestMapper
{
    public static InvestorRegistration ToRegistration(InvestorSignupRequest r)
    {
        var address = r.Address ?? new AddressRequest();
        var doc = r.IdentityDocument ?? new IdentityDocumentRequest();

        return new InvestorRegistration(
            FirstName: S(r.FirstName),
            LastName: S(r.LastName),
            Email: S(r.Email),
            BirthDate: S(r.BirthDate),
            BirthCity: r.BirthCity,
            BirthCountry: r.BirthCountry,
            Nationality: S(r.Nationality),
            Address: new AddressInfo(S(address.Line1), S(address.Postcode), S(address.City), S(address.Country)),
            PhoneNumber: r.PhoneNumber,
            TaxId: S(r.TaxId),
            TaxCountry: S(r.TaxCountry),
            IdentityDocument: new IdentityDocument(S(doc.Type), S(doc.Number), S(doc.IssuingCountry), S(doc.ExpiryDate)));
    }

    public static IReadOnlyList<ModelFund> ToFunds(IEnumerable<ModelFundDto>? funds) =>
        (funds ?? Enumerable.Empty<ModelFundDto>())
            .Select(f => new ModelFund(S(f.InstrumentId), f.Weight))
            .ToList();

    private static string S(string? value) => value ?? string.Empty;
}
