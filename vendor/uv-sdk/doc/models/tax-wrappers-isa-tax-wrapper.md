
# Tax Wrappers Isa Tax Wrapper

## Structure

`TaxWrappersIsaTaxWrapper`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Tax wrapper unique identifier. |
| `CreatedAt` | `DateTime` | Required | The date and time the tax wrapper was created. |
| `UpdatedAt` | `DateTime` | Required | The date and time the tax wrapper was last updated. |
| `AccountGroupId` | `Guid` | Required | Account group unique identifier. |
| `IsFlexible` | `bool` | Required | True if tax wrapper is flexible. |
| `Status` | [`Status56`](../../doc/models/status-56.md) | Required | Status of the tax wrapper<br><br>* NEW - The tax wrapper is newly created and not yet active due to pending checks.<br>* ACTIVE - The tax wrapper is currently active and in use.<br>* INACTIVE - The tax wrapper is not currently active. New subscriptions not allowed.<br>* CLOSED - The tax wrapper has been closed and is no longer available. |
| `Type` | `string` | Required, Constant | Types of the ISA tax wrapper<br><br>**Value**: `"STOCKS_AND_SHARES_ISA"` |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;

TaxWrappersIsaTaxWrapper taxWrappersIsaTaxWrapper = new TaxWrappersIsaTaxWrapper
{
    Id = new Guid("000011dc-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    AccountGroupId = new Guid("00002456-0000-0000-0000-000000000000"),
    IsFlexible = false,
    Status = Status56.New,
    Type = "STOCKS_AND_SHARES_ISA",
};
```

