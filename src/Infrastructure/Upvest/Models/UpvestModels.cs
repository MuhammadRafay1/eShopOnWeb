using System.Collections.Generic;

namespace Microsoft.eShopWeb.Infrastructure.Upvest.Models;

// Request/response DTOs for the Upvest Investment API. Property names are C# PascalCase and
// are mapped to/from the API's snake_case via a JsonNamingPolicy configured on the client.

// ---- Users ----

public record UpvestAddress(
    string AddressLine1,
    string Postcode,
    string City,
    string Country,
    string? AddressLine2 = null,
    string? State = null);

public record UpvestFatca(bool Status, string ConfirmedAt);

public record CreateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string BirthDate,
    IReadOnlyList<string> Nationalities,
    UpvestAddress Address,
    UpvestFatca Fatca,
    string? MiddleName = null,
    string? BirthCity = null,
    string? BirthCountry = null,
    string? PhoneNumber = null);

public record UserResponse(string Id, string Status);

// ---- Checks ----

public record CreateCheckRequest(
    string Type,
    string CheckConfirmedAt,
    string DocumentType,
    string DocumentExpirationDate,
    string Nationality);

public record CheckResponse(string Id);

// ---- Account groups & accounts ----

public record CreateAccountGroupRequest(string UserId, string Type);

public record AccountGroupResponse(string Id, string Status);

public record CreateAccountRequest(string UserId, string AccountGroupId, string Type, string Name);

public record AccountResponse(string Id, string Status);

// ---- Portfolio allocations (model portfolios) ----

public record AllocationEntry(string InstrumentId, string InstrumentIdType, string Weight);

public record CreateAllocationRequest(string Name, IReadOnlyList<AllocationEntry> Allocation);

public record AllocationResponse(string Id);

// ---- Portfolio configurations (which allocation an account follows) ----

public record CreateConfigurationRequest(string AccountId, string AllocationId);

public record ConfigurationResponse(string AccountId, string AllocationId);

// ---- Top-ups (sandbox funding) ----

public record CreateTopupRequest(string AccountGroupId, string CashAmount, string Currency);

public record TopupResponse(string Id, string Status);

// ---- Portfolio orders (investing a pay-in) ----

public record CreatePortfolioOrderRequest(string UserId, string AccountId, string CashAmount, string Currency, string Side);

public record PortfolioOrderResponse(string Id, string Status);

// ---- Rebalancing (realignment) ----

public record CreateRebalancingRequest(IReadOnlyList<string> Allocations);

public record RebalancingResponse(string Id);

public record RebalancingExecutionOrder(string Status, string? CancellationReason = null);

public record RebalancingOrdersResponse(IReadOnlyList<RebalancingExecutionOrder>? Data);

// ---- Valuations (portfolio holdings & value) ----

public record UpvestInstrumentRef(string? Isin, string? Uuid);

public record UpvestMoney(string? Amount, string? Currency);

public record UpvestSecurityPosition(
    UpvestInstrumentRef Instrument,
    UpvestMoney? Value,
    string? Quantity);

public record ValuationResponse(
    UpvestMoney? TotalSecurityValue,
    IReadOnlyList<UpvestSecurityPosition>? SecurityPositions);
