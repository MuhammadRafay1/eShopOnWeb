using System;
using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

// ----- Flow 1: becoming an investor -----

/// <summary>The shop's investor sign-up form (request body of POST /api/portfolio/investor).</summary>
public class SignUpInvestorRequest
{
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public DateOnly BirthDate { get; set; }
    public string BirthCity { get; set; } = default!;
    public string BirthCountry { get; set; } = default!;
    public string Nationality { get; set; } = default!;
    public AddressDto Address { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public string TaxId { get; set; } = default!;
    public string TaxCountry { get; set; } = default!;
    public IdentityDocumentDto IdentityDocument { get; set; } = default!;
}

public class AddressDto
{
    public string Line1 { get; set; } = default!;
    public string Postcode { get; set; } = default!;
    public string City { get; set; } = default!;
    public string Country { get; set; } = default!;
}

public class IdentityDocumentDto
{
    public string Type { get; set; } = default!;
    public string Number { get; set; } = default!;
    public string IssuingCountry { get; set; } = default!;
    public DateOnly ExpiryDate { get; set; }
}

public class InvestorResponse
{
    public Guid InvestorId { get; set; }
    public string Status { get; set; } = default!;
}

// ----- Flow 2: model portfolios -----

public class UpsertModelRequest
{
    public string Name { get; set; } = default!;
    public List<ModelFundDto> Funds { get; set; } = new();
}

public class ModelFundDto
{
    public string InstrumentId { get; set; } = default!;
    public decimal Weight { get; set; }
}

public class ModelResponse
{
    public string ModelId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public List<ModelFundDto> Funds { get; set; } = new();
}

public class ChooseModelRequest
{
    public string ModelId { get; set; } = default!;
}

// ----- Flow 3: paying in -----

public class PayInRequest
{
    public decimal Amount { get; set; }
}

public class PaymentResponse
{
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = default!;
}

/// <summary>Response of POST /api/portfolio/payments (paymentId + status only).</summary>
public class PaymentCreatedResponse
{
    public Guid PaymentId { get; set; }
    public string Status { get; set; } = default!;
}

// ----- Flow 4: realignment -----

public class RealignmentResponse
{
    public Guid RealignmentId { get; set; }
    public string Status { get; set; } = default!;
}

// ----- Flow 5: the portfolio -----

public class PortfolioHoldingDto
{
    public string InstrumentId { get; set; } = default!;
    public decimal Quantity { get; set; }
    public decimal Value { get; set; }
}

public class PortfolioResponse
{
    public string? ModelId { get; set; }
    public List<PortfolioHoldingDto> Holdings { get; set; } = new();
    public decimal TotalValue { get; set; }
    public decimal PaidIn { get; set; }
}

/// <summary>Maps application status enums to the fixed lowercase status strings the API contract requires.</summary>
public static class PortfolioStatusText
{
    public static string For(InvestorSignupStatus status) => status switch
    {
        InvestorSignupStatus.Active => "active",
        InvestorSignupStatus.Rejected => "rejected",
        _ => "pending"
    };

    public static string For(PaymentStatus status) => status switch
    {
        PaymentStatus.Invested => "invested",
        PaymentStatus.Failed => "failed",
        _ => "pending"
    };

    public static string For(RealignmentStatus status) => status switch
    {
        RealignmentStatus.Completed => "completed",
        RealignmentStatus.Failed => "failed",
        _ => "pending"
    };
}
