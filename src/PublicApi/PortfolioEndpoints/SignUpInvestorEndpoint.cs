using System;
using System.Globalization;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// POST /api/portfolio/investor — sign the signed-in shopper up as an investor with Upvest.
/// </summary>
public class SignUpInvestorEndpoint : IEndpoint<IResult, SignUpInvestorRequest, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (SignUpInvestorRequest request, ClaimsPrincipal user, IPortfolioService service, CancellationToken ct) =>
            {
                var buyerId = user.Identity?.Name;
                if (string.IsNullOrEmpty(buyerId)) return Results.Unauthorized();
                request.Bind(buyerId, ct);
                return await HandleAsync(request, service);
            })
            .Produces<InvestorResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(SignUpInvestorRequest request, IPortfolioService service)
    {
        var form = MapForm(request);
        var investor = await service.SignUpInvestorAsync(request.BuyerId, form, request.Cancellation);
        return Results.Ok(new InvestorResponse { InvestorId = investor.PublicId, Status = StatusText.For(investor.Status) });
    }

    private static InvestorSignUpForm MapForm(SignUpInvestorRequest r)
    {
        if (r.Address is null) throw new PortfolioValidationException("address is required.");
        if (r.IdentityDocument is null) throw new PortfolioValidationException("identityDocument is required.");

        return new InvestorSignUpForm(
            Required(r.FirstName, "firstName"),
            Required(r.LastName, "lastName"),
            Required(r.Email, "email"),
            ParseDate(r.BirthDate, "birthDate"),
            r.BirthCity,
            r.BirthCountry,
            Required(r.Nationality, "nationality"),
            new AddressForm(
                Required(r.Address.Line1, "address.line1"),
                Required(r.Address.Postcode, "address.postcode"),
                Required(r.Address.City, "address.city"),
                Required(r.Address.Country, "address.country")),
            r.PhoneNumber,
            Required(r.TaxId, "taxId"),
            Required(r.TaxCountry, "taxCountry"),
            new IdentityDocumentForm(
                Required(r.IdentityDocument.Type, "identityDocument.type"),
                Required(r.IdentityDocument.Number, "identityDocument.number"),
                Required(r.IdentityDocument.IssuingCountry, "identityDocument.issuingCountry"),
                ParseDate(r.IdentityDocument.ExpiryDate, "identityDocument.expiryDate")));
    }

    private static string Required(string? value, string field) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new PortfolioValidationException($"{field} is required.")
            : value.Trim();

    private static DateOnly ParseDate(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new PortfolioValidationException($"{field} must be an ISO-8601 date (YYYY-MM-DD).");
        }
        return date;
    }
}
