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

/// <summary>Opts the signed-in shopper in to investing their change (Flow 1).</summary>
public class CreateEnrolmentEndpoint : IEndpoint<IResult, EnrolmentRequest, IInvestingService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CreateEnrolmentEndpoint(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (EnrolmentRequest request, IInvestingService investingService) => await HandleAsync(request, investingService))
            .Produces<EnrolmentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(EnrolmentRequest request, IInvestingService investingService)
    {
        var buyerId = CallerId.Resolve(_httpContextAccessor.HttpContext?.User);
        if (string.IsNullOrEmpty(buyerId))
            return Results.Unauthorized();

        if (request is null ||
            string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Nationality) ||
            string.IsNullOrWhiteSpace(request.TaxId) || string.IsNullOrWhiteSpace(request.TaxCountry) ||
            request.Address is null || string.IsNullOrWhiteSpace(request.Address.Line1) ||
            string.IsNullOrWhiteSpace(request.Address.Postcode) || string.IsNullOrWhiteSpace(request.Address.City) ||
            string.IsNullOrWhiteSpace(request.Address.Country))
        {
            return Results.BadRequest(new { error = "All sign-up fields except phoneNumber are required." });
        }

        if (!DateOnly.TryParse(request.BirthDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthDate))
            return Results.BadRequest(new { error = "birthDate must be an ISO-8601 date (YYYY-MM-DD)." });

        var form = new InvestorSignupForm(
            request.FirstName.Trim(),
            request.LastName.Trim(),
            request.Email.Trim(),
            birthDate,
            request.Nationality.Trim().ToUpperInvariant(),
            new InvestorAddress(request.Address.Line1.Trim(), request.Address.Postcode.Trim(), request.Address.City.Trim(), request.Address.Country.Trim().ToUpperInvariant()),
            request.PhoneNumber?.Trim() ?? string.Empty,
            request.TaxId.Trim(),
            request.TaxCountry.Trim().ToUpperInvariant());

        var view = await investingService.EnrolAsync(buyerId, form, _httpContextAccessor.HttpContext!.RequestAborted);
        return Results.Ok(new EnrolmentResponse { EnrolmentId = view.EnrolmentId, Status = view.Status });
    }
}
