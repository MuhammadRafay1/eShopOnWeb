using System;
using System.Text.Json.Serialization;
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

/// <summary>
/// POST /api/investing/enrolment — opts the signed-in shopper in to investing their change,
/// creating them as an investor with Upvest.
/// </summary>
public class EnrolInvestorEndpoint : IEndpoint<IResult, EnrolInvestorRequest, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (EnrolInvestorRequest request, IInvestingService service, HttpContext http) =>
            {
                var shopperId = InvestingShared.ShopperId(http.User);
                if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();
                request.ShopperId = shopperId;
                return await HandleAsync(request, service);
            })
            .Produces<EnrolmentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(EnrolInvestorRequest request, IInvestingService service)
    {
        if (request.Address is null)
        {
            return Results.BadRequest("address is required.");
        }

        if (!DateOnly.TryParse(request.BirthDate, out var birthDate))
        {
            return Results.BadRequest("birthDate must be an ISO-8601 date (YYYY-MM-DD).");
        }

        var signup = new InvestorSignup(
            FirstName: request.FirstName,
            LastName: request.LastName,
            Email: request.Email,
            BirthDate: birthDate,
            Nationality: request.Nationality,
            Address: new InvestorAddress(request.Address.Line1, request.Address.Postcode, request.Address.City, request.Address.Country),
            PhoneNumber: request.PhoneNumber,
            TaxId: request.TaxId,
            TaxCountry: request.TaxCountry);

        var result = await service.EnrolAsync(request.ShopperId!, signup);
        return Results.Ok(new EnrolmentResponse(result.EnrolmentId, result.Status.ToText()));
    }
}

/// <summary>The shop's investor sign-up form. The shopper's identity comes from the token, not the body.</summary>
public class EnrolInvestorRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string BirthDate { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public AddressRequest? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string TaxId { get; set; } = string.Empty;
    public string TaxCountry { get; set; } = string.Empty;

    /// <summary>Set server-side from the token; never bound from the request body.</summary>
    [JsonIgnore]
    public string? ShopperId { get; set; }
}

public class AddressRequest
{
    public string Line1 { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public record EnrolmentResponse(Guid EnrolmentId, string Status);
