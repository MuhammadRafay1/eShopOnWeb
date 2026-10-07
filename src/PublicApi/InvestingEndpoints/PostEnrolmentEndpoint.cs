using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.Extensions.DependencyInjection;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// POST /api/investing/enrolment — opt the signed-in shopper in to investing their change.
/// </summary>
public class PostEnrolmentEndpoint : IEndpoint<IResult, EnrolmentRequest, HttpContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (EnrolmentRequest request, HttpContext http) => await HandleAsync(request, http))
            .Produces<EnrolmentResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(EnrolmentRequest request, HttpContext http)
    {
        var buyerId = InvestingEndpointHelpers.BuyerId(http.User);
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        if (!TryBuildDetails(request, out var details, out var error))
        {
            return Results.BadRequest(new { message = error });
        }

        var service = http.RequestServices.GetRequiredService<IInvestingService>();
        try
        {
            var view = await service.EnrolAsync(buyerId, details!, http.RequestAborted);
            return Results.Ok(new EnrolmentResponse { EnrolmentId = view.EnrolmentId, Status = view.Status.Wire() });
        }
        catch (Exception)
        {
            // Do not leak provider internals or personal data.
            return Results.Problem(
                title: "Enrolment with the investment provider could not be completed.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static bool TryBuildDetails(EnrolmentRequest request, out InvestorEnrolmentDetails? details, out string? error)
    {
        details = null;
        error = null;

        if (request is null)
        {
            error = "A sign-up form is required.";
            return false;
        }

        if (Missing(request.FirstName) || Missing(request.LastName) || Missing(request.Email) ||
            Missing(request.BirthDate) || Missing(request.Nationality) || Missing(request.PhoneNumber) ||
            Missing(request.TaxId) || Missing(request.TaxCountry) || request.Address is null ||
            Missing(request.Address.Line1) || Missing(request.Address.Postcode) ||
            Missing(request.Address.City) || Missing(request.Address.Country))
        {
            error = "firstName, lastName, email, birthDate, nationality, phoneNumber, taxId, taxCountry and a full address (line1, postcode, city, country) are required.";
            return false;
        }

        if (!DateOnly.TryParse(request.BirthDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthDate))
        {
            error = "birthDate must be an ISO-8601 date (yyyy-MM-dd).";
            return false;
        }

        if (!IsAlpha2(request.Nationality!) || !IsAlpha2(request.TaxCountry!) || !IsAlpha2(request.Address!.Country!))
        {
            error = "nationality, taxCountry and address.country must be ISO 3166-1 alpha-2 codes.";
            return false;
        }

        details = new InvestorEnrolmentDetails(
            FirstName: request.FirstName!.Trim(),
            LastName: request.LastName!.Trim(),
            Email: request.Email!.Trim(),
            BirthDate: birthDate,
            Nationality: request.Nationality!.Trim().ToUpperInvariant(),
            Address: new EnrolmentAddress(
                Line1: request.Address!.Line1!.Trim(),
                Postcode: request.Address.Postcode!.Trim(),
                City: request.Address.City!.Trim(),
                Country: request.Address.Country!.Trim().ToUpperInvariant()),
            PhoneNumber: request.PhoneNumber!.Trim(),
            TaxId: request.TaxId!.Trim(),
            TaxCountry: request.TaxCountry!.Trim().ToUpperInvariant());
        return true;
    }

    private static bool Missing(string? value) => string.IsNullOrWhiteSpace(value);

    private static bool IsAlpha2(string value)
        => value.Length == 2 && char.IsLetter(value[0]) && char.IsLetter(value[1]);
}
