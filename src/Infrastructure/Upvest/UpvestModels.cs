using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

// Request / response contracts for the Upvest Investment API. Property names map to Upvest's
// snake_case JSON via the shared snake-case naming policy; nullable members are omitted when null.
// Only fields the integration uses are modelled.

public sealed class UpvestAddress
{
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string Postcode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? State { get; set; }
    public string Country { get; set; } = string.Empty;
}

public sealed class UpvestConsent
{
    public string ConsentDocumentId { get; set; } = string.Empty;
    public string ConfirmedAt { get; set; } = string.Empty;
}

public sealed class UpvestFatca
{
    public bool Status { get; set; }
    public string ConfirmedAt { get; set; } = string.Empty;
}

public sealed class UpvestCreateUserRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string BirthDate { get; set; } = string.Empty;
    public string? BirthCity { get; set; }
    public string? BirthCountry { get; set; }
    public List<string> Nationalities { get; set; } = new();
    public UpvestAddress Address { get; set; } = new();
    public string? PhoneNumber { get; set; }
    public UpvestFatca Fatca { get; set; } = new();
    public UpvestConsent? TermsAndConditions { get; set; }
    public UpvestConsent? DataPrivacyAndSharingAgreement { get; set; }
}

public sealed class UpvestUser
{
    public string Id { get; set; } = string.Empty;
    public string? Status { get; set; }
}

public sealed class UpvestCreateKycCheckRequest
{
    public string Type { get; set; } = "KYC";
    public string CheckConfirmedAt { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string? DocumentExpirationDate { get; set; }
    public string? Nationality { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string? DataDownloadLink { get; set; }
}

public sealed class UpvestCheck
{
    public string Id { get; set; } = string.Empty;
    public string? Status { get; set; }
}

public sealed class UpvestTaxResidency
{
    public string Country { get; set; } = string.Empty;
    public string? TaxIdentifierNumber { get; set; }
    public string? MissingTinReason { get; set; }
}

public sealed class UpvestTaxResidenciesRequest
{
    public List<UpvestTaxResidency> TaxResidencies { get; set; } = new();
}

public sealed class UpvestCreateAccountGroupRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Type { get; set; } = "PERSONAL";
    public string? BusinessId { get; set; }
}

public sealed class UpvestAccountGroup
{
    public string Id { get; set; } = string.Empty;
    public string? Status { get; set; }
}

public sealed class UpvestCreateAccountRequest
{
    public string UserId { get; set; } = string.Empty;
    public string AccountGroupId { get; set; } = string.Empty;
    public string Type { get; set; } = "PORTFOLIO";
    public string? Name { get; set; }
    public string? BusinessId { get; set; }
}

public sealed class UpvestAccount
{
    public string Id { get; set; } = string.Empty;
    public string? AccountGroupId { get; set; }
    public string? Status { get; set; }
}

public sealed class UpvestAllocationEntry
{
    public string InstrumentId { get; set; } = string.Empty;
    public string InstrumentIdType { get; set; } = "ISIN";
    public string Weight { get; set; } = "0";
}

public sealed class UpvestAllocationRequest
{
    public string? Name { get; set; }
    public List<UpvestAllocationEntry> Allocation { get; set; } = new();
}

public sealed class UpvestAllocation
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public List<UpvestAllocationEntry> Allocation { get; set; } = new();
}

public sealed class UpvestConfigurationRequest
{
    public string AccountId { get; set; } = string.Empty;
    public string AllocationId { get; set; } = string.Empty;
}

public sealed class UpvestConfiguration
{
    public string AccountId { get; set; } = string.Empty;
    public string AllocationId { get; set; } = string.Empty;
}

public sealed class UpvestVirtualCashIncreaseRequest
{
    public string AccountGroupId { get; set; } = string.Empty;
    public string Amount { get; set; } = "0";
    public string Currency { get; set; } = "EUR";
}

public sealed class UpvestVirtualCashIncrease
{
    public string Id { get; set; } = string.Empty;
    public string? Status { get; set; }
}

public sealed class UpvestPortfolioOrderRequest
{
    public string UserId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string CashAmount { get; set; } = "0";
    public string Currency { get; set; } = "EUR";
    public string Side { get; set; } = "BUY";
}

public sealed class UpvestPortfolioOrder
{
    public string Id { get; set; } = string.Empty;
    public string? Status { get; set; }
}

public sealed class UpvestRebalancingRequest
{
    public List<string> Allocations { get; set; } = new();
}

public sealed class UpvestRebalancingExecution
{
    public string Id { get; set; } = string.Empty;
}

public sealed class UpvestRebalancingOrder
{
    public string Id { get; set; } = string.Empty;
    public string? Status { get; set; }
}

public sealed class UpvestRebalancingOrderList
{
    public List<UpvestRebalancingOrder> Data { get; set; } = new();
}

public sealed class UpvestMoney
{
    public string Amount { get; set; } = "0";
    public string? Currency { get; set; }
}

public sealed class UpvestInstrument
{
    public string? Uuid { get; set; }
    public string? Isin { get; set; }
}

public sealed class UpvestSecurityPosition
{
    public UpvestInstrument? Instrument { get; set; }
    public string? Quantity { get; set; }
    public UpvestMoney? Value { get; set; }
    public string? Weight { get; set; }
}

public sealed class UpvestValuation
{
    public string? Id { get; set; }
    public string? AccountId { get; set; }

    [JsonPropertyName("total_security_value")]
    public UpvestMoney? TotalSecurityValue { get; set; }

    public List<UpvestSecurityPosition> SecurityPositions { get; set; } = new();
}
