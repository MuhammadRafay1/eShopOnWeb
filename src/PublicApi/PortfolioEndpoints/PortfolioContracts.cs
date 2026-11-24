using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

// ----- Requests -------------------------------------------------------------------------------

public record RegisterInvestorRequest(
    string FirstName,
    string LastName,
    string Email,
    string BirthDate,
    string BirthCity,
    string BirthCountry,
    string Nationality,
    AddressRequest Address,
    string PhoneNumber,
    string TaxId,
    string TaxCountry,
    IdentityDocumentRequest IdentityDocument);

public record AddressRequest(string Line1, string Postcode, string City, string Country);

public record IdentityDocumentRequest(string Type, string Number, string IssuingCountry, string ExpiryDate);

public record UpsertModelRequest(string Name, List<FundRequest> Funds);

public record FundRequest(string InstrumentId, decimal Weight);

public record ChooseModelRequest(string ModelId);

public record CreatePaymentRequest(decimal Amount);

// Commands combine a route parameter with the body so a single HandleAsync signature can carry both.
public record UpsertModelCommand(string ModelId, string Name, List<FundRequest> Funds);

public record TriggerRealignmentCommand(string ModelId);

public record GetRealignmentCommand(string RealignmentId);

// ----- Responses ------------------------------------------------------------------------------

public record InvestorResponse(string InvestorId, string Status);

public record FundResponse(string InstrumentId, decimal Weight);

public record ModelResponse(string ModelId, string Name, IReadOnlyList<FundResponse> Funds);

public record PaymentResponse(string PaymentId, decimal Amount, string Status);

public record RealignmentResponse(string RealignmentId, string Status);

public record HoldingResponse(string InstrumentId, decimal Quantity, decimal Value);

public record PortfolioResponse(
    string? ModelId,
    IReadOnlyList<HoldingResponse> Holdings,
    decimal TotalValue,
    decimal PaidIn);

// ----- Mapping helpers ------------------------------------------------------------------------

internal static class PortfolioMapping
{
    /// <summary>The caller's storefront identity, taken from the JWT (ClaimTypes.Name = user name).</summary>
    public static string? ShopperId(ClaimsPrincipal? user) =>
        user?.FindFirstValue(ClaimTypes.Name) ?? user?.Identity?.Name;

    /// <summary>Rounds money to two decimal places and keeps a two-place scale for the JSON number.</summary>
    public static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero) + 0.00m;

    public static string Status(InvestorStatus status) => status switch
    {
        InvestorStatus.Active => "active",
        InvestorStatus.Rejected => "rejected",
        _ => "pending"
    };

    public static string Status(PaymentStatus status) => status switch
    {
        PaymentStatus.Invested => "invested",
        PaymentStatus.Failed => "failed",
        _ => "pending"
    };

    public static string Status(RealignmentStatus status) => status switch
    {
        RealignmentStatus.Completed => "completed",
        RealignmentStatus.Failed => "failed",
        _ => "pending"
    };

    public static InvestorResponse ToResponse(this Investor investor) =>
        new(investor.UpvestUserId ?? investor.Id.ToString(), Status(investor.Status));

    public static ModelResponse ToResponse(this ModelPortfolio model) =>
        new(model.ModelId, model.Name,
            model.Funds.Select(f => new FundResponse(f.InstrumentId, f.Weight)).ToList());

    public static PaymentResponse ToResponse(this PortfolioPayment payment) =>
        new(payment.PublicId, Money(payment.Amount), Status(payment.Status));

    public static RealignmentResponse ToResponse(this Realignment realignment) =>
        new(realignment.PublicId, Status(realignment.Status));
}
