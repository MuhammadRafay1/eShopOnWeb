using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.PublicApi.Investing;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>The shop's investor sign-up form.</summary>
public class EnrolRequest
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
}

public class EnrolAddress
{
    public string Line1 { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class EnrolmentResponse
{
    public Guid EnrolmentId { get; set; }
    public string Status { get; set; } = string.Empty;

    public static EnrolmentResponse From(Investor investor) => new()
    {
        EnrolmentId = investor.EnrolmentId,
        Status = investor.Status.ToString().ToLowerInvariant(),
    };
}

public class InvestmentDto
{
    public Guid InvestmentId { get; set; }

    [JsonConverter(typeof(TwoDecimalMoneyConverter))]
    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;
}

public class BalanceResponse
{
    [JsonConverter(typeof(TwoDecimalMoneyConverter))]
    public decimal PendingAmount { get; set; }

    [JsonConverter(typeof(TwoDecimalMoneyConverter))]
    public decimal InvestedAmount { get; set; }
}
