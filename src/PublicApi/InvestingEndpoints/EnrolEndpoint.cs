using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Opt the signed-in shopper in to investing their spare change.</summary>
public class EnrolEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (EnrolRequest request, ClaimsPrincipal user, IInvestingService investing, CancellationToken cancellationToken) =>
            {
                var shopperId = user.Identity?.Name;
                if (string.IsNullOrEmpty(shopperId))
                    return Results.Unauthorized();

                if (!TryBuildDetails(request, out var details, out var error))
                    return Results.BadRequest(new { error });

                var investor = await investing.EnrolAsync(shopperId, details, cancellationToken);
                return Results.Ok(EnrolmentResponse.From(investor));
            })
            .Produces<EnrolmentResponse>()
            .ProducesValidationProblem()
            .WithTags("InvestingEndpoints");
    }

    private static bool TryBuildDetails(EnrolRequest request, out EnrolmentDetails details, out string? error)
    {
        details = null!;
        error = null;

        if (string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.BirthDate) ||
            string.IsNullOrWhiteSpace(request.Nationality) ||
            request.Address is null ||
            string.IsNullOrWhiteSpace(request.Address.Line1) ||
            string.IsNullOrWhiteSpace(request.Address.Postcode) ||
            string.IsNullOrWhiteSpace(request.Address.City) ||
            string.IsNullOrWhiteSpace(request.Address.Country))
        {
            error = "firstName, lastName, email, birthDate, nationality and a full address are required.";
            return false;
        }

        details = new EnrolmentDetails(
            request.FirstName,
            request.LastName,
            request.Email,
            request.BirthDate,
            request.Nationality,
            new EnrolmentAddress(request.Address.Line1, request.Address.Postcode, request.Address.City, request.Address.Country),
            request.PhoneNumber,
            request.TaxId,
            request.TaxCountry);
        return true;
    }
}
