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
using Microsoft.eShopWeb.Infrastructure.Investing;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Flow 1 — opt the signed-in shopper in to investing their change.</summary>
public class EnrolEndpoint : IEndpoint<IResult>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (EnrolmentRequest request, ClaimsPrincipal user, IInvestingService service, CancellationToken ct) =>
            {
                var buyerId = InvestingMapping.BuyerId(user);
                if (string.IsNullOrWhiteSpace(buyerId)) return Results.Unauthorized();

                if (!TryBuildDetails(request, out var details, out var error))
                    return Results.BadRequest(new { message = error });

                try
                {
                    var view = await service.EnrolAsync(buyerId, details, ct);
                    return Results.Ok(new EnrolmentResponse
                    {
                        EnrolmentId = view.EnrolmentId,
                        Status = InvestingMapping.ToWire(view.Status),
                    });
                }
                catch (UpvestProviderException ex)
                {
                    return InvestingMapping.MapProviderError(ex);
                }
            })
            .Produces<EnrolmentResponse>()
            .WithTags("InvestingEndpoints");
    }

    private static bool TryBuildDetails(EnrolmentRequest request, out InvestorEnrolmentDetails details, out string error)
    {
        details = null!;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Nationality) ||
            request.Address is null || string.IsNullOrWhiteSpace(request.Address.Line1) ||
            string.IsNullOrWhiteSpace(request.Address.Postcode) || string.IsNullOrWhiteSpace(request.Address.City) ||
            string.IsNullOrWhiteSpace(request.Address.Country) ||
            string.IsNullOrWhiteSpace(request.TaxId) || string.IsNullOrWhiteSpace(request.TaxCountry))
        {
            error = "firstName, lastName, email, birthDate, nationality, address (line1/postcode/city/country), taxId and taxCountry are required.";
            return false;
        }

        if (!DateOnly.TryParse(request.BirthDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthDate))
        {
            error = "birthDate must be an ISO-8601 date (YYYY-MM-DD).";
            return false;
        }

        details = new InvestorEnrolmentDetails(
            request.FirstName.Trim(),
            request.LastName.Trim(),
            request.Email.Trim(),
            birthDate,
            request.Nationality.Trim().ToUpperInvariant(),
            request.Address.Line1.Trim(),
            request.Address.Postcode.Trim(),
            request.Address.City.Trim(),
            request.Address.Country.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            request.TaxId.Trim(),
            request.TaxCountry.Trim().ToUpperInvariant());
        return true;
    }

    // Required by IEndpoint<IResult>; routing invokes AddRoute, not this.
    public Task<IResult> HandleAsync() => Task.FromResult<IResult>(Results.Empty);
}
