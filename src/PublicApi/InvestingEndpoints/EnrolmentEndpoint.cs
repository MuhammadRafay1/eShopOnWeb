using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.PublicApi.Investing;
using Microsoft.eShopWeb.PublicApi.Investing.Upvest;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// POST /api/investing/enrolment — opt the signed-in shopper in to investing their change.
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
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (EnrolmentRequest request, IInvestingService investingService) =>
            {
                return await HandleAsync(request, investingService);
            })
            .Produces<EnrolmentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(EnrolmentRequest request, IInvestingService investingService)
    {
        var buyerId = CurrentUser.BuyerId(_httpContextAccessor.HttpContext?.User);
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        var validationError = Validate(request);
        if (validationError is not null)
        {
            return Results.BadRequest(new { error = validationError });
        }

        var details = new EnrolmentDetails(
            request.FirstName.Trim(),
            request.LastName.Trim(),
            request.Email.Trim(),
            request.BirthDate.Trim(),
            request.Nationality.Trim().ToUpperInvariant(),
            new EnrolmentAddress(
                request.Address.Line1.Trim(),
                request.Address.Postcode.Trim(),
                request.Address.City.Trim(),
                request.Address.Country.Trim().ToUpperInvariant()),
            request.PhoneNumber.Trim(),
            request.TaxId.Trim(),
            request.TaxCountry.Trim().ToUpperInvariant());

        try
        {
            var result = await investingService.EnrolAsync(buyerId, details);
            var response = new EnrolmentResponse(request.CorrelationId())
            {
                EnrolmentId = result.EnrolmentId,
                Status = result.Status
            };
            return Results.Ok(response);
        }
        catch (UpvestException)
        {
            // Never surface personal data or upstream internals.
            return Results.Problem(
                title: "The investment provider could not process the enrolment. Please try again later.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static string? Validate(EnrolmentRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.FirstName) || string.IsNullOrWhiteSpace(r.LastName))
            return "firstName and lastName are required.";
        if (string.IsNullOrWhiteSpace(r.Email))
            return "email is required.";
        if (string.IsNullOrWhiteSpace(r.BirthDate))
            return "birthDate is required.";
        if (string.IsNullOrWhiteSpace(r.Nationality) || r.Nationality.Trim().Length != 2)
            return "nationality must be an ISO 3166-1 alpha-2 code.";
        if (r.Address is null || string.IsNullOrWhiteSpace(r.Address.Line1) ||
            string.IsNullOrWhiteSpace(r.Address.Postcode) || string.IsNullOrWhiteSpace(r.Address.City) ||
            string.IsNullOrWhiteSpace(r.Address.Country))
            return "address line1, postcode, city and country are required.";
        if (string.IsNullOrWhiteSpace(r.TaxId) || string.IsNullOrWhiteSpace(r.TaxCountry))
            return "taxId and taxCountry are required.";
        return null;
    }
}
