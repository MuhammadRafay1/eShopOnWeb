using System;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

public record SignUpInvestorCommand(string ShopperId, SignUpInvestorRequest Body);

/// <summary>
/// Flow 1 — signs the signed-in shopper up as an investor with Upvest.
/// </summary>
public class SignUpInvestorEndpoint : IEndpoint<IResult, SignUpInvestorCommand, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (SignUpInvestorRequest request, ClaimsPrincipal user, IPortfolioService service) =>
            {
                var shopperId = user.GetShopperId();
                if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();
                return await HandleAsync(new SignUpInvestorCommand(shopperId, request), service);
            })
            .Produces<InvestorResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(SignUpInvestorCommand command, IPortfolioService service)
    {
        var form = MapToSignUp(command.Body);
        var view = await service.SignUpInvestorAsync(command.ShopperId, form);
        return Results.Ok(view.ToResponse());
    }

    private static InvestorSignUp MapToSignUp(SignUpInvestorRequest r)
    {
        if (r is null) throw new PortfolioValidationException("A sign-up form is required.");
        Require(r.FirstName, "firstName");
        Require(r.LastName, "lastName");
        Require(r.Nationality, "nationality");
        Require(r.Address?.Line1, "address.line1");
        Require(r.Address?.Postcode, "address.postcode");
        Require(r.Address?.City, "address.city");
        Require(r.Address?.Country, "address.country");
        Require(r.IdentityDocument?.Type, "identityDocument.type");
        Require(r.IdentityDocument?.Number, "identityDocument.number");
        Require(r.IdentityDocument?.IssuingCountry, "identityDocument.issuingCountry");

        return new InvestorSignUp(
            r.FirstName,
            r.LastName,
            r.Email,
            ParseDate(r.BirthDate, "birthDate"),
            r.BirthCity,
            r.BirthCountry,
            r.Nationality,
            new SignUpAddress(r.Address.Line1, r.Address.Postcode, r.Address.City, r.Address.Country),
            r.PhoneNumber,
            r.TaxId,
            r.TaxCountry,
            new SignUpIdentityDocument(
                r.IdentityDocument.Type,
                r.IdentityDocument.Number,
                r.IdentityDocument.IssuingCountry,
                ParseDate(r.IdentityDocument.ExpiryDate, "identityDocument.expiryDate")));
    }

    private static void Require(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new PortfolioValidationException($"'{field}' is required.");
    }

    private static DateOnly ParseDate(string value, string field)
    {
        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        throw new PortfolioValidationException($"'{field}' must be an ISO-8601 date (YYYY-MM-DD).");
    }
}
