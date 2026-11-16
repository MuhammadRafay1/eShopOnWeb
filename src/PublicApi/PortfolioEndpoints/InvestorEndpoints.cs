using System;
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

public sealed class SignUpInvestorRequestEnvelope : ShopperRequest
{
    public SignUpInvestorRequest Body { get; set; } = default!;
}

/// <summary>
/// POST /api/portfolio/investor — signs the signed-in shopper up as an investor with Upvest.
/// </summary>
public sealed class SignUpInvestorEndpoint : IEndpoint<IResult, SignUpInvestorRequestEnvelope, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (SignUpInvestorRequest body, HttpContext http, IPortfolioService service) =>
                await HandleAsync(new SignUpInvestorRequestEnvelope
                {
                    Body = body,
                    ShopperId = PortfolioEndpointHelpers.ShopperId(http),
                    CancellationToken = http.RequestAborted
                }, service))
            .Produces<InvestorResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(SignUpInvestorRequestEnvelope request, IPortfolioService service)
    {
        var signUp = MapSignUp(request.Body);
        var result = await service.SignUpInvestorAsync(request.ShopperId, signUp, request.CancellationToken);
        return Results.Ok(result.ToResponse());
    }

    internal static InvestorSignUp MapSignUp(SignUpInvestorRequest b)
    {
        if (b is null) throw PortfolioException.BadRequest("A request body is required.");
        Require(b.FirstName, "firstName");
        Require(b.LastName, "lastName");
        Require(b.Email, "email");
        Require(b.Nationality, "nationality");
        if (b.Address is null) throw PortfolioException.BadRequest("address is required.");
        Require(b.Address.Line1, "address.line1");
        Require(b.Address.Postcode, "address.postcode");
        Require(b.Address.City, "address.city");
        Require(b.Address.Country, "address.country");
        if (b.IdentityDocument is null) throw PortfolioException.BadRequest("identityDocument is required.");
        Require(b.IdentityDocument.Type, "identityDocument.type");
        Require(b.IdentityDocument.Number, "identityDocument.number");
        Require(b.IdentityDocument.IssuingCountry, "identityDocument.issuingCountry");

        return new InvestorSignUp(
            b.FirstName!.Trim(),
            b.LastName!.Trim(),
            b.Email!.Trim(),
            b.BirthDate,
            b.BirthCity,
            b.BirthCountry,
            b.Nationality!.Trim().ToUpperInvariant(),
            new PostalAddress(b.Address.Line1!.Trim(), b.Address.Postcode!.Trim(), b.Address.City!.Trim(), b.Address.Country!.Trim().ToUpperInvariant()),
            b.PhoneNumber,
            b.TaxId,
            string.IsNullOrWhiteSpace(b.TaxCountry) ? null : b.TaxCountry!.Trim().ToUpperInvariant(),
            new IdentityDocument(
                b.IdentityDocument.Type!.Trim(),
                b.IdentityDocument.Number!.Trim(),
                b.IdentityDocument.IssuingCountry!.Trim().ToUpperInvariant(),
                b.IdentityDocument.ExpiryDate));
    }

    private static void Require(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw PortfolioException.BadRequest($"{field} is required.");
    }
}

public sealed class GetInvestorRequest : ShopperRequest { }

/// <summary>
/// GET /api/portfolio/investor — reports where the caller's sign-up has got to.
/// </summary>
public sealed class GetInvestorEndpoint : IEndpoint<IResult, GetInvestorRequest, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (HttpContext http, IPortfolioService service) =>
                await HandleAsync(new GetInvestorRequest
                {
                    ShopperId = PortfolioEndpointHelpers.ShopperId(http),
                    CancellationToken = http.RequestAborted
                }, service))
            .Produces<InvestorResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(GetInvestorRequest request, IPortfolioService service)
    {
        var result = await service.GetInvestorAsync(request.ShopperId, request.CancellationToken);
        return result is null
            ? Results.NotFound()
            : Results.Ok(result.ToResponse());
    }
}
