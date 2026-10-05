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
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;
using Microsoft.Extensions.DependencyInjection;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Opts the signed-in shopper in to investing their change (POST /api/investing/enrolment).</summary>
public class EnrollInvestorEndpoint : IEndpoint<IResult, EnrolmentRequest, HttpContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (EnrolmentRequest request, HttpContext http) => await HandleAsync(request, http))
            .Produces<EnrolmentResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(EnrolmentRequest request, HttpContext http)
    {
        var shopperId = http.User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();

        if (request is null) return Results.BadRequest(new { error = "A request body is required." });
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return Results.BadRequest(new { error = "firstName and lastName are required." });
        if (string.IsNullOrWhiteSpace(request.Email))
            return Results.BadRequest(new { error = "email is required." });
        if (!TryParseBirthDate(request.BirthDate, out var birthDate))
            return Results.BadRequest(new { error = "birthDate must be an ISO-8601 date (YYYY-MM-DD)." });
        if (!IsAlpha2(request.Nationality))
            return Results.BadRequest(new { error = "nationality must be an ISO 3166-1 alpha-2 code." });
        if (request.Address is null ||
            string.IsNullOrWhiteSpace(request.Address.Line1) ||
            string.IsNullOrWhiteSpace(request.Address.Postcode) ||
            string.IsNullOrWhiteSpace(request.Address.City) ||
            !IsAlpha2(request.Address.Country))
            return Results.BadRequest(new { error = "address.line1, address.postcode, address.city and an alpha-2 address.country are required." });
        if (string.IsNullOrWhiteSpace(request.TaxId) || !IsAlpha2(request.TaxCountry))
            return Results.BadRequest(new { error = "taxId and an alpha-2 taxCountry are required." });

        var details = new InvestorSignUpDetails
        {
            FirstName = request.FirstName!.Trim(),
            LastName = request.LastName!.Trim(),
            Email = request.Email!.Trim(),
            BirthDate = birthDate,
            Nationality = request.Nationality!.Trim().ToUpperInvariant(),
            AddressLine1 = request.Address!.Line1!.Trim(),
            Postcode = request.Address.Postcode!.Trim(),
            City = request.Address.City!.Trim(),
            Country = request.Address.Country!.Trim().ToUpperInvariant(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber!.Trim(),
            TaxId = request.TaxId!.Trim(),
            TaxCountry = request.TaxCountry!.Trim().ToUpperInvariant(),
        };

        var service = http.RequestServices.GetRequiredService<IInvestingService>();
        var enrolment = await service.EnrolAsync(shopperId, details, http.RequestAborted);

        return Results.Ok(new EnrolmentResponse
        {
            EnrolmentId = enrolment.Id,
            Status = InvestingStatusText.For(enrolment.Status),
        });
    }

    private static bool TryParseBirthDate(string? value, out DateTimeOffset birthDate)
    {
        birthDate = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out birthDate);
    }

    private static bool IsAlpha2(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length == 2;
}
