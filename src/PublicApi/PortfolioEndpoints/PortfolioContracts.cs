using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

// ---- Request DTOs (the shop's forms) ----

public class InvestorSignupApiRequest
{
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public DateOnly BirthDate { get; set; }
    public string BirthCity { get; set; } = default!;
    public string BirthCountry { get; set; } = default!;
    public string Nationality { get; set; } = default!;
    public AddressApi Address { get; set; } = default!;
    public string PhoneNumber { get; set; } = default!;
    public string TaxId { get; set; } = default!;
    public string TaxCountry { get; set; } = default!;
    public IdentityDocumentApi IdentityDocument { get; set; } = default!;
}

public class AddressApi
{
    public string Line1 { get; set; } = default!;
    public string Postcode { get; set; } = default!;
    public string City { get; set; } = default!;
    public string Country { get; set; } = default!;
}

public class IdentityDocumentApi
{
    public string Type { get; set; } = default!;
    public string Number { get; set; } = default!;
    public string IssuingCountry { get; set; } = default!;
    public DateOnly ExpiryDate { get; set; }
}

public class FundApi
{
    public string InstrumentId { get; set; } = default!;
    public decimal Weight { get; set; }
}

public class ModelUpsertApiRequest
{
    public string Name { get; set; } = default!;
    public List<FundApi> Funds { get; set; } = new();
}

public class ChooseModelApiRequest
{
    public string ModelId { get; set; } = default!;
}

public class PayInApiRequest
{
    public decimal Amount { get; set; }
}

// ---- Response DTOs (fixed field names per the task) ----

public class InvestorApiResponse
{
    public Guid? InvestorId { get; set; }
    public string Status { get; set; } = default!;
}

public class ModelApiResponse
{
    public string ModelId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public List<FundApi> Funds { get; set; } = new();
}

public class PaymentApiResponse
{
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = default!;
}

public class RealignmentApiResponse
{
    public Guid RealignmentId { get; set; }
    public string Status { get; set; } = default!;
}

public class HoldingApi
{
    public string InstrumentId { get; set; } = default!;
    public decimal Quantity { get; set; }
    public decimal Value { get; set; }
}

public class PortfolioApiResponse
{
    public string? ModelId { get; set; }
    public List<HoldingApi> Holdings { get; set; } = new();
    public decimal TotalValue { get; set; }
    public decimal PaidIn { get; set; }
}

/// <summary>Shared mapping between the domain layer and the API wire shape.</summary>
public static class PortfolioApi
{
    /// <summary>The signed-in shopper's stable identity (from the JWT). The sole key for per-shopper isolation.</summary>
    public static string? ShopperId(System.Security.Claims.ClaimsPrincipal user) => user.Identity?.Name;

    public static string Wire(InvestorSignupStatus s) => s.ToString().ToLowerInvariant();
    public static string Wire(PaymentStatus s) => s.ToString().ToLowerInvariant();
    public static string Wire(RealignmentStatus s) => s.ToString().ToLowerInvariant();

    /// <summary>A euro amount as a two-decimal-place JSON number.</summary>
    public static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero) + 0.00m;

    public static InvestorApiResponse ToResponse(InvestorStatusResult r) =>
        new() { InvestorId = r.InvestorId, Status = Wire(r.Status) };

    public static ModelApiResponse ToResponse(ModelResult r) =>
        new()
        {
            ModelId = r.ModelId,
            Name = r.Name,
            Funds = r.Funds.Select(f => new FundApi { InstrumentId = f.InstrumentId, Weight = f.Weight }).ToList()
        };

    public static PaymentApiResponse ToResponse(PaymentResult r) =>
        new() { PaymentId = r.PaymentId, Amount = Money(r.Amount), Status = Wire(r.Status) };

    public static RealignmentApiResponse ToResponse(RealignmentResult r) =>
        new() { RealignmentId = r.RealignmentId, Status = Wire(r.Status) };

    public static PortfolioApiResponse ToResponse(PortfolioView r) =>
        new()
        {
            ModelId = r.ModelId,
            Holdings = r.Holdings.Select(h => new HoldingApi
            {
                InstrumentId = h.InstrumentId,
                Quantity = h.Quantity,
                Value = Money(h.Value)
            }).ToList(),
            TotalValue = Money(r.TotalValue),
            PaidIn = Money(r.PaidIn)
        };

    /// <summary>Turns a domain exception into a caller-safe HTTP result (no provider detail, no PII).</summary>
    public static ActionResult ToErrorResult(PortfolioException ex)
    {
        var (status, title) = ex switch
        {
            PortfolioValidationException => (StatusCodes.Status400BadRequest, "Invalid request"),
            PortfolioNotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            PortfolioConflictException => (StatusCodes.Status409Conflict, "Not allowed yet"),
            PortfolioUnknownOutcomeException => (StatusCodes.Status502BadGateway, "Outcome not confirmed"),
            PortfolioProviderException p => (MapProviderStatus(p.StatusCode), "Provider error"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error")
        };

        return new ObjectResult(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = ex.Message
        })
        { StatusCode = status };
    }

    // Our-fault/throttle (401/403/429) and outages become 5xx; a caller-fixable 4xx is passed through.
    private static int MapProviderStatus(HttpStatusCode? status) => (int?)status switch
    {
        401 or 403 => StatusCodes.Status502BadGateway,
        429 => StatusCodes.Status503ServiceUnavailable,
        >= 400 and < 500 => (int)status!,
        _ => StatusCodes.Status502BadGateway
    };
}
