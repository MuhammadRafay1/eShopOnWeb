using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Signs the signed-in shopper up as an investor with Upvest.</summary>
public class RegisterInvestorEndpoint : IEndpoint<IResult, RegisterInvestorRequest, IManagedPortfolioService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RegisterInvestorEndpoint(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (RegisterInvestorRequest request, IManagedPortfolioService service) =>
                await HandleAsync(request, service))
            .Produces<InvestorResponse>(StatusCodes.Status201Created)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(RegisterInvestorRequest request, IManagedPortfolioService service)
    {
        var shopperId = PortfolioMapping.ShopperId(_httpContextAccessor.HttpContext?.User);
        if (string.IsNullOrEmpty(shopperId))
        {
            return Results.Unauthorized();
        }

        if (request is null)
        {
            return Results.BadRequest("A sign-up body is required.");
        }
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
        {
            return Results.BadRequest("firstName and lastName are required.");
        }
        if (request.Address is null)
        {
            return Results.BadRequest("address is required.");
        }
        if (request.IdentityDocument is null)
        {
            return Results.BadRequest("identityDocument is required.");
        }
        if (!TryParseDate(request.BirthDate, out var birthDate))
        {
            return Results.BadRequest("birthDate must be an ISO-8601 date (yyyy-MM-dd).");
        }
        if (!TryParseDate(request.IdentityDocument.ExpiryDate, out var expiryDate))
        {
            return Results.BadRequest("identityDocument.expiryDate must be an ISO-8601 date (yyyy-MM-dd).");
        }

        var signUp = new InvestorSignUp(
            request.FirstName, request.LastName, request.Email, birthDate, request.BirthCity,
            request.BirthCountry, request.Nationality,
            new AddressInput(request.Address.Line1, request.Address.Postcode, request.Address.City, request.Address.Country),
            request.PhoneNumber, request.TaxId, request.TaxCountry,
            new IdentityDocumentInput(request.IdentityDocument.Type, request.IdentityDocument.Number, request.IdentityDocument.IssuingCountry, expiryDate));

        var investor = await service.SignUpInvestorAsync(shopperId, signUp);
        return Results.Created("api/portfolio/investor", investor.ToResponse());
    }

    private static bool TryParseDate(string? value, out DateOnly date) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}

/// <summary>Reports where the caller's investor sign-up has got to at Upvest.</summary>
public class GetInvestorEndpoint : IEndpoint<IResult, IManagedPortfolioService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetInvestorEndpoint(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IManagedPortfolioService service) => await HandleAsync(service))
            .Produces<InvestorResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(IManagedPortfolioService service)
    {
        var shopperId = PortfolioMapping.ShopperId(_httpContextAccessor.HttpContext?.User);
        if (string.IsNullOrEmpty(shopperId))
        {
            return Results.Unauthorized();
        }

        var investor = await service.GetInvestorAsync(shopperId);
        return investor is null
            ? Results.NotFound("The caller has not signed up as an investor.")
            : Results.Ok(investor.ToResponse());
    }
}
