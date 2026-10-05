using System;
using System.Globalization;
using System.Security.Claims;
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

/// <summary>Opt the signed-in shopper in to investing their change.</summary>
public class EnrolEndpoint : IEndpoint<IResult, EnrolmentRequest, IInvestingService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EnrolEndpoint(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (EnrolmentRequest request, IInvestingService service) => await HandleAsync(request, service))
            .Produces<EnrolmentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(EnrolmentRequest request, IInvestingService service)
    {
        var httpContext = _httpContextAccessor.HttpContext!;
        var buyerId = httpContext.User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrEmpty(buyerId)) return Results.Unauthorized();
        var cancellationToken = httpContext.RequestAborted;

        var validation = Validate(request, out var birthDate);
        if (validation is not null) return Results.BadRequest(new { error = validation });

        var signUp = new InvestorSignUp(
            request.FirstName.Trim(),
            request.LastName.Trim(),
            request.Email.Trim(),
            birthDate,
            request.Nationality.Trim(),
            new InvestorAddress(request.Address.Line1.Trim(), request.Address.Postcode.Trim(), request.Address.City.Trim(), request.Address.Country.Trim()),
            request.PhoneNumber?.Trim() ?? string.Empty,
            request.TaxId.Trim(),
            request.TaxCountry.Trim());

        var view = await service.EnrolAsync(buyerId, signUp, cancellationToken);
        return Results.Ok(new EnrolmentResponse
        {
            EnrolmentId = view.EnrolmentId,
            Status = InvestingStatusText.ToWire(view.Status)
        });
    }

    private static string? Validate(EnrolmentRequest r, out DateTimeOffset birthDate)
    {
        birthDate = default;
        if (string.IsNullOrWhiteSpace(r.FirstName)) return "firstName is required.";
        if (string.IsNullOrWhiteSpace(r.LastName)) return "lastName is required.";
        if (string.IsNullOrWhiteSpace(r.Email)) return "email is required.";
        if (string.IsNullOrWhiteSpace(r.Nationality) || r.Nationality.Trim().Length != 2) return "nationality must be an ISO 3166-1 alpha-2 code.";
        if (r.Address is null) return "address is required.";
        if (string.IsNullOrWhiteSpace(r.Address.Line1)) return "address.line1 is required.";
        if (string.IsNullOrWhiteSpace(r.Address.Postcode)) return "address.postcode is required.";
        if (string.IsNullOrWhiteSpace(r.Address.City)) return "address.city is required.";
        if (string.IsNullOrWhiteSpace(r.Address.Country) || r.Address.Country.Trim().Length != 2) return "address.country must be an ISO 3166-1 alpha-2 code.";
        if (string.IsNullOrWhiteSpace(r.TaxId)) return "taxId is required.";
        if (string.IsNullOrWhiteSpace(r.TaxCountry) || r.TaxCountry.Trim().Length != 2) return "taxCountry must be an ISO 3166-1 alpha-2 code.";
        if (!DateTimeOffset.TryParse(r.BirthDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out birthDate))
            return "birthDate must be an ISO-8601 date.";
        return null;
    }
}
