using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Flow 1 — signs the signed-in shopper up as an investor with Upvest.
/// </summary>
public class SignUpInvestorEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (SignUpInvestorRequest request, ClaimsPrincipal user, IManagedPortfolioService service, CancellationToken ct) =>
            {
                return await HandleAsync(request, user, service, ct);
            })
            .Produces<object>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(SignUpInvestorRequest request, ClaimsPrincipal user, IManagedPortfolioService service, CancellationToken ct)
    {
        var shopperId = CallerIdentity.GetShopperId(user);
        if (string.IsNullOrEmpty(shopperId))
        {
            return Results.Unauthorized();
        }

        var validationError = Validate(request);
        if (validationError is not null)
        {
            return Results.BadRequest(new { message = validationError });
        }

        var signUp = new InvestorSignUp(
            FirstName: request.FirstName,
            LastName: request.LastName,
            Email: request.Email,
            BirthDate: request.BirthDate,
            BirthCity: request.BirthCity,
            BirthCountry: request.BirthCountry,
            Nationality: request.Nationality,
            Address: new AddressInput(request.Address!.Line1, request.Address.Postcode, request.Address.City, request.Address.Country),
            PhoneNumber: request.PhoneNumber,
            TaxId: request.TaxId,
            TaxCountry: request.TaxCountry,
            IdentityDocument: new IdentityDocumentInput(
                request.IdentityDocument!.Type, request.IdentityDocument.Number,
                request.IdentityDocument.IssuingCountry, request.IdentityDocument.ExpiryDate));

        var result = await service.SignUpInvestorAsync(shopperId, signUp, ct);
        return Results.Ok(new { investorId = result.InvestorId, status = result.Status });
    }

    private static string? Validate(SignUpInvestorRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.FirstName)) return "firstName is required.";
        if (string.IsNullOrWhiteSpace(r.LastName)) return "lastName is required.";
        if (string.IsNullOrWhiteSpace(r.Email)) return "email is required.";
        if (string.IsNullOrWhiteSpace(r.BirthDate)) return "birthDate is required.";
        if (string.IsNullOrWhiteSpace(r.Nationality)) return "nationality is required.";
        if (r.Address is null
            || string.IsNullOrWhiteSpace(r.Address.Line1)
            || string.IsNullOrWhiteSpace(r.Address.Postcode)
            || string.IsNullOrWhiteSpace(r.Address.City)
            || string.IsNullOrWhiteSpace(r.Address.Country))
        {
            return "A complete address (line1, postcode, city, country) is required.";
        }
        if (r.IdentityDocument is null
            || string.IsNullOrWhiteSpace(r.IdentityDocument.Type)
            || string.IsNullOrWhiteSpace(r.IdentityDocument.Number)
            || string.IsNullOrWhiteSpace(r.IdentityDocument.IssuingCountry)
            || string.IsNullOrWhiteSpace(r.IdentityDocument.ExpiryDate))
        {
            return "A complete identityDocument (type, number, issuingCountry, expiryDate) is required.";
        }
        return null;
    }
}
