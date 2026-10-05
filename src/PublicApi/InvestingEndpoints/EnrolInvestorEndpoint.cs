using System.Security.Claims;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.PublicApi.Investing;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Opts the signed-in shopper in to investing their change (Flow 1). The shopper is registered
/// with Upvest and becomes an investor able to hold what is bought for them. Enrolment is
/// idempotent per shopper. The response is "pending" until Upvest accepts the shopper.
/// </summary>
public class EnrolInvestorEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                EnrolmentRequest request,
                ClaimsPrincipal user,
                IInvestorOnboardingService onboarding) =>
                await HandleAsync(request, user, onboarding))
            .Produces<EnrolmentResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("InvestingEndpoints");
    }

    private static async Task<IResult> HandleAsync(EnrolmentRequest request, ClaimsPrincipal user, IInvestorOnboardingService onboarding)
    {
        var buyerId = user.Identity?.Name;
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));

        var validationError = Validate(request);
        if (validationError is not null)
        {
            return Results.BadRequest(validationError);
        }

        var registration = new InvestorRegistration(
            request.FirstName,
            request.LastName,
            request.Email,
            request.BirthDate,
            request.Nationality,
            new RegistrationAddress(request.Address.Line1, request.Address.Postcode, request.Address.City, request.Address.Country),
            request.PhoneNumber,
            request.TaxId,
            request.TaxCountry);

        var account = await onboarding.EnrolAsync(buyerId!, registration);

        return Results.Ok(new EnrolmentResponse(request.CorrelationId())
        {
            EnrolmentId = account.EnrolmentId,
            Status = account.Status.ToText()
        });
    }

    private static string? Validate(EnrolmentRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.FirstName) || string.IsNullOrWhiteSpace(r.LastName))
        {
            return "First and last name are required.";
        }
        if (string.IsNullOrWhiteSpace(r.Email))
        {
            return "Email is required.";
        }
        if (r.BirthDate == default)
        {
            return "A valid birth date is required.";
        }
        if (r.Nationality is not { Length: 2 })
        {
            return "Nationality must be an ISO 3166-1 alpha-2 code.";
        }
        if (string.IsNullOrWhiteSpace(r.Address.Line1) || string.IsNullOrWhiteSpace(r.Address.Postcode)
            || string.IsNullOrWhiteSpace(r.Address.City) || r.Address.Country is not { Length: 2 })
        {
            return "A full address (line1, postcode, city and alpha-2 country) is required.";
        }
        if (r.TaxCountry is not { Length: 2 })
        {
            return "Tax country must be an ISO 3166-1 alpha-2 code.";
        }
        return null;
    }
}
