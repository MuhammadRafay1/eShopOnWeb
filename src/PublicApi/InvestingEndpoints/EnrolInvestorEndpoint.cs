using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.Extensions.DependencyInjection;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Opts the signed-in shopper in to investing their change, registering them as an investor with Upvest.
/// </summary>
public class EnrolInvestorEndpoint : IEndpoint<IResult, EnrolmentRequest, HttpContext>
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
        var shopper = InvestingContractMapping.ResolveShopper(http);
        if (string.IsNullOrEmpty(shopper))
            return Results.Unauthorized();

        if (!TryValidate(request, out var error))
            return Results.BadRequest(new { error });

        var registration = new UpvestInvestorRegistration(
            request.FirstName.Trim(),
            request.LastName.Trim(),
            request.Email.Trim(),
            request.BirthDate.Trim(),
            request.Nationality.Trim().ToUpperInvariant(),
            request.Address.Line1.Trim(),
            request.Address.Postcode.Trim(),
            request.Address.City.Trim(),
            request.Address.Country.Trim().ToUpperInvariant(),
            request.PhoneNumber.Trim(),
            request.TaxId.Trim(),
            request.TaxCountry.Trim().ToUpperInvariant());

        var investing = http.RequestServices.GetRequiredService<IInvestingService>();
        var enrolment = await investing.EnrolAsync(shopper, registration, http.RequestAborted);

        return Results.Ok(new EnrolmentResponse
        {
            EnrolmentId = enrolment.EnrolmentId,
            Status = InvestingContractMapping.ToApi(enrolment.Status),
        });
    }

    private static bool TryValidate(EnrolmentRequest r, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(r.FirstName) || string.IsNullOrWhiteSpace(r.LastName))
            error = "firstName and lastName are required.";
        else if (string.IsNullOrWhiteSpace(r.Email))
            error = "email is required.";
        else if (string.IsNullOrWhiteSpace(r.BirthDate))
            error = "birthDate is required (ISO-8601 date).";
        else if (string.IsNullOrWhiteSpace(r.Nationality) || r.Nationality.Trim().Length != 2)
            error = "nationality must be an ISO 3166-1 alpha-2 code.";
        else if (r.Address is null || string.IsNullOrWhiteSpace(r.Address.Line1) || string.IsNullOrWhiteSpace(r.Address.Postcode)
                 || string.IsNullOrWhiteSpace(r.Address.City) || string.IsNullOrWhiteSpace(r.Address.Country))
            error = "address.line1, address.postcode, address.city and address.country are required.";
        else if (string.IsNullOrWhiteSpace(r.PhoneNumber))
            error = "phoneNumber is required.";
        else if (string.IsNullOrWhiteSpace(r.TaxId) || string.IsNullOrWhiteSpace(r.TaxCountry))
            error = "taxId and taxCountry are required.";

        return error.Length == 0;
    }
}
