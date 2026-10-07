using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

// ---- Enrolment ----

/// <summary>The shop's investor sign-up form.</summary>
public class EnrolmentRequest : BaseRequest
{
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;
    /// <summary>ISO-8601 date, e.g. 1990-01-31.</summary>
    public string BirthDate { get; set; } = default!;
    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public string Nationality { get; set; } = default!;
    public EnrolmentAddress Address { get; set; } = default!;
    public string PhoneNumber { get; set; } = default!;
    public string TaxId { get; set; } = default!;
    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public string TaxCountry { get; set; } = default!;
}

public class EnrolmentAddress
{
    public string Line1 { get; set; } = default!;
    public string Postcode { get; set; } = default!;
    public string City { get; set; } = default!;
    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public string Country { get; set; } = default!;
}

public class EnrolmentResponse : BaseResponse
{
    public EnrolmentResponse(Guid correlationId) : base(correlationId) { }
    public EnrolmentResponse() { }

    public int EnrolmentId { get; set; }
    public string Status { get; set; } = default!;
}

// ---- Orders ----

public class PlaceOrderRequest : BaseRequest
{
    public List<OrderItemRequest> Items { get; set; } = new();
}

public class OrderItemRequest
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

public class PlaceOrderResponse : BaseResponse
{
    public PlaceOrderResponse(Guid correlationId) : base(correlationId) { }
    public PlaceOrderResponse() { }

    public int OrderId { get; set; }
    public decimal RoundUpAmount { get; set; }
}

// ---- Balance ----

public class BalanceResponse : BaseResponse
{
    public BalanceResponse(Guid correlationId) : base(correlationId) { }
    public BalanceResponse() { }

    public decimal PendingAmount { get; set; }
    public decimal InvestedAmount { get; set; }
}

// ---- Investments ----

public class InvestmentsResponse : BaseResponse
{
    public InvestmentsResponse(Guid correlationId) : base(correlationId) { }
    public InvestmentsResponse() { }

    public List<InvestmentDto> Investments { get; set; } = new();
}

public class InvestmentDto
{
    public int InvestmentId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = default!;
}
