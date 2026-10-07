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

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Opts the signed-in shopper in to investing their change, registering them as an investor at Upvest.
/// </summary>
public class EnrolmentEndpoint : IEndpoint<IResult, EnrolmentRequest, IInvestingService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EnrolmentEndpoint(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (EnrolmentRequest request, IInvestingService investing) =>
                await HandleAsync(request, investing))
            .Produces<EnrolmentResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(EnrolmentRequest request, IInvestingService investing)
    {
        var buyerId = CallerIdentity.GetBuyerId(_httpContextAccessor);
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        if (!TryBuildDetails(request, out var details, out var error))
        {
            return Results.BadRequest(error);
        }

        var investor = await investing.EnrolAsync(buyerId, details!);

        return Results.Ok(new EnrolmentResponse(request.CorrelationId())
        {
            EnrolmentId = investor.PublicId,
            Status = InvestingStatus.ToApi(investor.Status),
        });
    }

    private static bool TryBuildDetails(EnrolmentRequest request, out InvestorEnrolmentDetails? details, out string? error)
    {
        details = null;
        error = null;

        if (string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Email))
        {
            error = "firstName, lastName and email are required.";
            return false;
        }

        if (!DateOnly.TryParse(request.BirthDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthDate))
        {
            error = "birthDate must be an ISO-8601 date (e.g. 1990-05-17).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Nationality) || request.Nationality.Trim().Length != 2)
        {
            error = "nationality must be an ISO 3166-1 alpha-2 code.";
            return false;
        }

        var address = request.Address;
        if (address == null ||
            string.IsNullOrWhiteSpace(address.Line1) ||
            string.IsNullOrWhiteSpace(address.Postcode) ||
            string.IsNullOrWhiteSpace(address.City) ||
            string.IsNullOrWhiteSpace(address.Country))
        {
            error = "address.line1, address.postcode, address.city and address.country are required.";
            return false;
        }

        details = new InvestorEnrolmentDetails(
            FirstName: request.FirstName.Trim(),
            LastName: request.LastName.Trim(),
            Email: request.Email.Trim(),
            BirthDate: birthDate,
            Nationality: request.Nationality.Trim().ToUpperInvariant(),
            Address: new InvestorAddress(address.Line1.Trim(), address.Postcode.Trim(), address.City.Trim(), address.Country.Trim().ToUpperInvariant()),
            PhoneNumber: string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            TaxId: string.IsNullOrWhiteSpace(request.TaxId) ? null : request.TaxId.Trim(),
            TaxCountry: string.IsNullOrWhiteSpace(request.TaxCountry) ? null : request.TaxCountry.Trim().ToUpperInvariant());
        return true;
    }
}
