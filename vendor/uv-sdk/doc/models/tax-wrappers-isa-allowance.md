
# Tax Wrappers Isa Allowance

## Structure

`TaxWrappersIsaAllowance`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Allowance unique identifier. |
| `CreatedAt` | `DateTime` | Required | The date and time the tax wrapper was created. |
| `UpdatedAt` | `DateTime` | Required | The date and time the tax wrapper was last updated. |
| `TaxWrapperId` | `Guid` | Required | Tax wrapper unique identifier. |
| `TaxYear` | `string` | Optional | **Constraints**: *Pattern*: `^[0-9]{4}/[0-9]{4}$` |
| `Type` | `string` | Required, Constant | Type of the allowance:<br><br>* ANNUAL - Allowance valid for a tax year.<br><br>**Value**: `"ANNUAL"` |
| `Status` | [`Status57`](../../doc/models/status-57.md) | Required | Status of the allowance:<br><br>* ACTIVE - Current allowance.<br>* EXPIRED - Allowance from previous years. |
| `Currency` | `string` | Required, Constant | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* GBP - British Pound Sterling<br><br>**Value**: `"GBP"` |
| `UsedAmount` | `string` | Required | **Constraints**: *Pattern*: `^-?[0-9]{1,9}(\.[0-9]{2})?$` |
| `RemainingAmount` | `string` | Required | **Constraints**: *Pattern*: `^-?[0-9]{1,9}(\.[0-9]{2})?$` |
| `ValidFrom` | `DateTime` | Required | The date from which the allowance is valid. |
| `ValidTo` | `DateTime?` | Optional | - |
| `FirstSubscriptionAt` | `DateTime?` | Optional | - |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;

TaxWrappersIsaAllowance taxWrappersIsaAllowance = new TaxWrappersIsaAllowance
{
    Id = new Guid("00001ff6-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    TaxWrapperId = new Guid("0000094a-0000-0000-0000-000000000000"),
    Type = "ANNUAL",
    Status = Status57.Active,
    Currency = "GBP",
    UsedAmount = "used_amount0",
    RemainingAmount = "remaining_amount2",
    ValidFrom = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    TaxYear = "tax_year4",
    ValidTo = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    FirstSubscriptionAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
};
```

