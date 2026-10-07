
# Virtual Cash Balance Virtual Cash Increase

## Structure

`VirtualCashBalanceVirtualCashIncrease`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Virtual cash unique identifier |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `AccountGroupId` | `Guid` | Required | Account group unique identifier. |
| `Amount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | [`Currency1`](../../doc/models/currency-1.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling<br>* USD - The United States dollar |
| `Status` | [`Status86`](../../doc/models/status-86.md) | Required | Status of the virtual cash<br><br>* ISSUED - Virtual cash increase is created.<br>* CONFIRMED - Virtual cash increase was successfully processed.<br>* CANCELLED - Virtual cash increase was cancelled. |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;

VirtualCashBalanceVirtualCashIncrease virtualCashBalanceVirtualCashIncrease = new VirtualCashBalanceVirtualCashIncrease
{
    Id = new Guid("00000a86-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    AccountGroupId = new Guid("00001d00-0000-0000-0000-000000000000"),
    Amount = "amount6",
    Currency = Currency1.Usd,
    Status = Status86.Cancelled,
};
```

