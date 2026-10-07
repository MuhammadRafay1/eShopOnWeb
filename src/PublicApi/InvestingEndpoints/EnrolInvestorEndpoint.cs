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
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Logging;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// POST /api/investing/enrolment — opt the signed-in shopper in to investing their change.
/// </summary>
public class EnrolInvestorEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (EnrolInvestorRequest request, ClaimsPrincipal user,
                   IInvestingService investingService, ILoggerFactory loggerFactory, CancellationToken ct) =>
            {
                var buyerId = Caller.BuyerId(user);
                if (string.IsNullOrEmpty(buyerId))
                {
                    return Results.Unauthorized();
                }

                if (!TryBuildSignUp(request, out var signUp, out var error))
                {
                    return Results.BadRequest(new { error });
                }

                try
                {
                    var view = await investingService.EnrolAsync(buyerId, signUp, ct);
                    return Results.Ok(new EnrolmentResponse { EnrolmentId = view.EnrolmentId, Status = view.Status });
                }
                catch (Exception ex)
                {
                    // Never log personal details — only the failure itself.
                    loggerFactory.CreateLogger<EnrolInvestorEndpoint>()
                        .LogError("Enrolment with Upvest failed: {Message}", ex.Message);
                    return Results.Problem(
                        title: "Enrolment could not be completed with the investment provider.",
                        statusCode: StatusCodes.Status502BadGateway);
                }
            })
            .Produces<EnrolmentResponse>()
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .WithTags("InvestingEndpoints");
    }

    private static bool TryBuildSignUp(EnrolInvestorRequest request, out InvestorSignUp signUp, out string error)
    {
        signUp = null!;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
        {
            error = "firstName and lastName are required.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            error = "email is required.";
            return false;
        }
        if (!DateOnly.TryParse(request.BirthDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthDate))
        {
            error = "birthDate must be an ISO-8601 date (yyyy-MM-dd).";
            return false;
        }
        if (string.IsNullOrWhiteSpace(request.Nationality) || request.Nationality.Length != 2)
        {
            error = "nationality must be an ISO 3166-1 alpha-2 code.";
            return false;
        }
        if (request.Address is null
            || string.IsNullOrWhiteSpace(request.Address.Line1)
            || string.IsNullOrWhiteSpace(request.Address.Postcode)
            || string.IsNullOrWhiteSpace(request.Address.City)
            || string.IsNullOrWhiteSpace(request.Address.Country))
        {
            error = "address.line1, address.postcode, address.city and address.country are required.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(request.TaxCountry) || request.TaxCountry.Length != 2)
        {
            error = "taxCountry must be an ISO 3166-1 alpha-2 code.";
            return false;
        }

        signUp = new InvestorSignUp(
            request.FirstName.Trim(),
            request.LastName.Trim(),
            request.Email.Trim(),
            birthDate,
            request.Nationality.Trim().ToUpperInvariant(),
            new SignUpAddress(
                request.Address.Line1.Trim(),
                request.Address.Postcode.Trim(),
                request.Address.City.Trim(),
                request.Address.Country.Trim().ToUpperInvariant()),
            request.PhoneNumber?.Trim() ?? string.Empty,
            request.TaxId?.Trim() ?? string.Empty,
            request.TaxCountry.Trim().ToUpperInvariant());
        return true;
    }
}
