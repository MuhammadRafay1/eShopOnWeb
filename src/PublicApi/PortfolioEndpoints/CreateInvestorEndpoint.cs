using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Flow 1 — signs the signed-in shopper up as an investor with Upvest.
/// </summary>
public class CreateInvestorEndpoint : PortfolioEndpointBase, IEndpoint<IResult, CreateInvestorRequest>
{
    public CreateInvestorEndpoint(IHttpContextAccessor accessor) : base(accessor) { }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreateInvestorRequest request) => await HandleAsync(request))
            .Produces<InvestorResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(CreateInvestorRequest request)
    {
        var shopperId = ShopperId;
        if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();

        var (signup, errors) = Validate(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var investors = Service<IRepository<Investor>>();
        var upvest = Service<IUpvestService>();

        // One investor per shopper. If already signed up, report the existing record.
        var existing = await investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), Ct);
        if (existing is not null)
        {
            return Results.Ok(ToResponse(existing, request.CorrelationId(),
                existing.StatusDetail ?? "You are already registered as an investor."));
        }

        var result = await upvest.OnboardInvestorAsync(signup!, Ct);

        var investor = new Investor(shopperId, result.UpvestUserId);
        if (result.UpvestAccountGroupId is not null) investor.LinkAccountGroup(result.UpvestAccountGroupId);
        if (result.UpvestAccountId is not null) investor.LinkAccount(result.UpvestAccountId);
        investor.SetStatus(result.Status, result.Detail);
        await investors.AddAsync(investor, Ct);

        return Results.Ok(ToResponse(investor, request.CorrelationId(), result.Detail));
    }

    internal static InvestorResponse ToResponse(Investor investor, Guid correlationId, string? detail) => new(correlationId)
    {
        InvestorId = investor.UpvestUserId,
        Status = investor.Status.ToString().ToLowerInvariant(),
        Detail = detail ?? investor.StatusDetail
    };

    private static (InvestorSignup?, Dictionary<string, string[]>) Validate(CreateInvestorRequest r)
    {
        var errors = new Dictionary<string, string[]>();
        void Require(string? value, string field)
        {
            if (string.IsNullOrWhiteSpace(value)) errors[field] = new[] { $"{field} is required." };
        }

        Require(r.FirstName, nameof(r.FirstName));
        Require(r.LastName, nameof(r.LastName));
        Require(r.Email, nameof(r.Email));
        Require(r.BirthCity, nameof(r.BirthCity));
        Require(r.BirthCountry, nameof(r.BirthCountry));
        Require(r.Nationality, nameof(r.Nationality));
        Require(r.PhoneNumber, nameof(r.PhoneNumber));
        Require(r.TaxId, nameof(r.TaxId));
        Require(r.TaxCountry, nameof(r.TaxCountry));

        if (r.Address is null) errors[nameof(r.Address)] = new[] { "Address is required." };
        else
        {
            Require(r.Address.Line1, "Address.Line1");
            Require(r.Address.Postcode, "Address.Postcode");
            Require(r.Address.City, "Address.City");
            Require(r.Address.Country, "Address.Country");
        }

        if (r.IdentityDocument is null) errors[nameof(r.IdentityDocument)] = new[] { "IdentityDocument is required." };
        else
        {
            Require(r.IdentityDocument.Type, "IdentityDocument.Type");
            Require(r.IdentityDocument.Number, "IdentityDocument.Number");
            Require(r.IdentityDocument.IssuingCountry, "IdentityDocument.IssuingCountry");
        }

        if (!TryParseDate(r.BirthDate, out var birthDate)) errors[nameof(r.BirthDate)] = new[] { "BirthDate must be an ISO-8601 date (yyyy-MM-dd)." };
        DateOnly expiry = default;
        if (r.IdentityDocument is not null && !TryParseDate(r.IdentityDocument.ExpiryDate, out expiry))
        {
            errors["IdentityDocument.ExpiryDate"] = new[] { "ExpiryDate must be an ISO-8601 date (yyyy-MM-dd)." };
        }

        if (errors.Count > 0) return (null, errors);

        var signup = new InvestorSignup(
            r.FirstName!, r.LastName!, r.Email!, birthDate, r.BirthCity!, r.BirthCountry!, r.Nationality!,
            new SignupAddress(r.Address!.Line1!, r.Address.Postcode!, r.Address.City!, r.Address.Country!),
            r.PhoneNumber!, r.TaxId!, r.TaxCountry!,
            new IdentityDocument(r.IdentityDocument!.Type!, r.IdentityDocument.Number!, r.IdentityDocument.IssuingCountry!, expiry));
        return (signup, errors);
    }

    private static bool TryParseDate(string? value, out DateOnly date) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}
