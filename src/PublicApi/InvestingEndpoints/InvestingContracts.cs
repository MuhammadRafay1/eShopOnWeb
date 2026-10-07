using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>A request that carries only the caller's identity (taken from the token), for the GET endpoints.</summary>
public class CallerRequest : BaseRequest
{
    [JsonIgnore]
    public string BuyerId { get; set; } = string.Empty;
}

public class AddressRequest
{
    public string Line1 { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class EnrolmentRequest : BaseRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>ISO-8601 date, e.g. 1990-05-21.</summary>
    public string BirthDate { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2, e.g. DE.</summary>
    public string Nationality { get; set; } = string.Empty;

    public AddressRequest Address { get; set; } = new();
    public string PhoneNumber { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string TaxCountry { get; set; } = string.Empty;

    [JsonIgnore]
    public string BuyerId { get; set; } = string.Empty;
}

public class EnrolmentResponse : BaseResponse
{
    public EnrolmentResponse(Guid correlationId) : base(correlationId)
    {
    }

    public EnrolmentResponse()
    {
    }

    public int EnrolmentId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class BalanceResponse : BaseResponse
{
    public BalanceResponse(Guid correlationId) : base(correlationId)
    {
    }

    public BalanceResponse()
    {
    }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal PendingAmount { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal InvestedAmount { get; set; }
}

public class InvestmentDto
{
    public int InvestmentId { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;
}

public class InvestmentsResponse : BaseResponse
{
    public InvestmentsResponse(Guid correlationId) : base(correlationId)
    {
    }

    public InvestmentsResponse()
    {
    }

    public List<InvestmentDto> Investments { get; set; } = new();
}

/// <summary>Maps domain statuses to the fixed lowercase strings the API returns.</summary>
public static class InvestingStatusText
{
    public static string ToApiString(this EnrolmentStatus status) => status switch
    {
        EnrolmentStatus.Active => "active",
        EnrolmentStatus.Rejected => "rejected",
        _ => "pending"
    };

    public static string ToApiString(this InvestmentStatus status) => status switch
    {
        InvestmentStatus.Settled => "settled",
        InvestmentStatus.Failed => "failed",
        _ => "pending"
    };
}
