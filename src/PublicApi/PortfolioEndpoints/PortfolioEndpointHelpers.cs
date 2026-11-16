using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

internal static class PortfolioEndpointHelpers
{
    /// <summary>The signed-in shopper's identity (the JWT name claim). The caller only ever acts for themselves.</summary>
    public static string? GetUserName(ClaimsPrincipal user) =>
        user.Identity?.Name ?? user.FindFirstValue(ClaimTypes.Name);

    /// <summary>
    /// Maps an Upvest failure to an HTTP response without leaking any request data. Genuine caller
    /// errors (400/404/409/422) are passed through; scope/auth (401/403) and server errors become 502,
    /// since those reflect the integration's own standing with Upvest, not the caller's request.
    /// </summary>
    public static IResult ToResult(UpvestException ex) => ex.StatusCode switch
    {
        400 or 422 => Results.BadRequest(new { message = ex.Message }),
        404 => Results.NotFound(new { message = ex.Message }),
        409 => Results.Conflict(new { message = ex.Message }),
        _ => Results.Problem(title: "Upvest request failed", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway)
    };

    public static InvestorSignup ToSignup(SignUpInvestorRequest r) => new(
        r.FirstName!, r.LastName!, r.Email!, r.BirthDate!, r.BirthCity!, r.BirthCountry!, r.Nationality!,
        new InvestorAddress(r.Address!.Line1!, r.Address.Postcode!, r.Address.City!, r.Address.Country!),
        r.PhoneNumber,
        r.TaxId!, r.TaxCountry!,
        new IdentityDocument(r.IdentityDocument!.Type!, r.IdentityDocument.Number!, r.IdentityDocument.IssuingCountry!, r.IdentityDocument.ExpiryDate!));

    /// <summary>Validates the sign-up form; returns the list of missing/invalid fields.</summary>
    public static List<string> ValidateSignup(SignUpInvestorRequest r)
    {
        var errors = new List<string>();
        void Req(string? v, string name) { if (string.IsNullOrWhiteSpace(v)) errors.Add(name); }

        Req(r.FirstName, "firstName");
        Req(r.LastName, "lastName");
        Req(r.Email, "email");
        Req(r.BirthDate, "birthDate");
        Req(r.BirthCity, "birthCity");
        Req(r.BirthCountry, "birthCountry");
        Req(r.Nationality, "nationality");
        Req(r.TaxId, "taxId");
        Req(r.TaxCountry, "taxCountry");

        if (r.Address is null) errors.Add("address");
        else
        {
            Req(r.Address.Line1, "address.line1");
            Req(r.Address.Postcode, "address.postcode");
            Req(r.Address.City, "address.city");
            Req(r.Address.Country, "address.country");
        }

        if (r.IdentityDocument is null) errors.Add("identityDocument");
        else
        {
            Req(r.IdentityDocument.Type, "identityDocument.type");
            Req(r.IdentityDocument.Number, "identityDocument.number");
            Req(r.IdentityDocument.IssuingCountry, "identityDocument.issuingCountry");
            Req(r.IdentityDocument.ExpiryDate, "identityDocument.expiryDate");
        }

        return errors;
    }
}
