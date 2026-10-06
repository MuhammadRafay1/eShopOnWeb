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

public class EnrolRequest : BaseRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string BirthDate { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public EnrolAddress Address { get; set; } = new();
    public string PhoneNumber { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string TaxCountry { get; set; } = string.Empty;

    [JsonIgnore]
    public string BuyerId { get; set; } = string.Empty;
}

public class EnrolAddress
{
    public string Line1 { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class EnrolmentResponse : BaseResponse
{
    public EnrolmentResponse(Guid correlationId) : base(correlationId) { }
    public EnrolmentResponse() { }

    public int EnrolmentId { get; set; }
    public string Status { get; set; } = "pending";
}

/// <summary>
/// Opts the signed-in shopper in to investing their change, registering them as an investor with
/// Upvest. The enrolment starts "pending" until Upvest accepts the shopper.
/// </summary>
public class EnrolEndpoint : IEndpoint<IResult, EnrolRequest, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (EnrolRequest request, IInvestingService investing, HttpContext httpContext) =>
            {
                request.BuyerId = httpContext.User.Identity?.Name ?? string.Empty;
                return await HandleAsync(request, investing);
            })
            .Produces<EnrolmentResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(EnrolRequest request, IInvestingService investing)
    {
        if (string.IsNullOrEmpty(request.BuyerId))
        {
            return Results.Unauthorized();
        }

        var missing = Validate(request);
        if (missing is not null)
        {
            return Results.BadRequest(missing);
        }

        var signup = new InvestorSignup(
            request.FirstName.Trim(),
            request.LastName.Trim(),
            request.Email.Trim(),
            request.BirthDate.Trim(),
            request.Nationality.Trim(),
            new SignupAddress(
                request.Address.Line1.Trim(),
                request.Address.Postcode.Trim(),
                request.Address.City.Trim(),
                request.Address.Country.Trim()),
            request.PhoneNumber.Trim(),
            request.TaxId.Trim(),
            request.TaxCountry.Trim());

        try
        {
            var investor = await investing.EnrolAsync(request.BuyerId, signup, default);
            var response = new EnrolmentResponse(request.CorrelationId())
            {
                EnrolmentId = investor.Id,
                Status = InvestingStatusText.For(investor.Status)
            };
            return Results.Ok(response);
        }
        catch (Exception)
        {
            // Never surface provider internals (which could include error payloads); keep it generic.
            return Results.Problem("Could not complete enrolment with the investment provider. Please try again.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static string? Validate(EnrolRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.FirstName)) return "firstName is required.";
        if (string.IsNullOrWhiteSpace(r.LastName)) return "lastName is required.";
        if (string.IsNullOrWhiteSpace(r.Email)) return "email is required.";
        if (string.IsNullOrWhiteSpace(r.BirthDate)) return "birthDate is required.";
        if (string.IsNullOrWhiteSpace(r.Nationality)) return "nationality is required.";
        if (r.Address is null || string.IsNullOrWhiteSpace(r.Address.Line1)) return "address.line1 is required.";
        if (string.IsNullOrWhiteSpace(r.Address.Postcode)) return "address.postcode is required.";
        if (string.IsNullOrWhiteSpace(r.Address.City)) return "address.city is required.";
        if (string.IsNullOrWhiteSpace(r.Address.Country)) return "address.country is required.";
        if (string.IsNullOrWhiteSpace(r.PhoneNumber)) return "phoneNumber is required.";
        if (string.IsNullOrWhiteSpace(r.TaxId)) return "taxId is required.";
        if (string.IsNullOrWhiteSpace(r.TaxCountry)) return "taxCountry is required.";
        return null;
    }
}
