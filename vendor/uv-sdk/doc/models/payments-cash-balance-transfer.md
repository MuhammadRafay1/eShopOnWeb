
# Payments Cash Balance Transfer

Represents an internal cash balance transfer between two account groups belonging to the same user and tenant. Only settled cash in the same currency can be transferred.

## Structure

`PaymentsCashBalanceTransfer`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Cash balance transfer unique identifier |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `SourceAccountGroupId` | `Guid` | Required | The account group the cash is transferred from. Must differ from `target_account_group_id` and belong to the same user and tenant. Allowed account group types are `PERSONAL`, `CHILD`, and `BUSINESS`, and the source and target account groups must be of the same type. |
| `TargetAccountGroupId` | `Guid` | Required | The account group the cash is transferred to. Must differ from `source_account_group_id` and belong to the same user and tenant. Allowed account group types are `PERSONAL`, `CHILD`, and `BUSINESS`, and the source and target account groups must be of the same type. |
| `Amount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | [`Currency`](../../doc/models/currency.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |
| `Status` | [`Status37`](../../doc/models/status-37.md) | Required | Status of the cash balance transfer.<br><br>* ISSUED - Transfer has been created and the cash movement is in progress.<br>* CONFIRMED - Cash was successfully moved from the source to the target account group.<br>* CANCELLED - Transfer was cancelled and no cash was moved. |
| `CancellationReason` | `string` | Optional | Reason the transfer was cancelled. Present only when `status` is `CANCELLED`, otherwise `null`. |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;

PaymentsCashBalanceTransfer paymentsCashBalanceTransfer = new PaymentsCashBalanceTransfer
{
    Id = new Guid("0000053e-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    SourceAccountGroupId = new Guid("00001146-0000-0000-0000-000000000000"),
    TargetAccountGroupId = new Guid("000006a2-0000-0000-0000-000000000000"),
    Amount = "amount4",
    Currency = Currency.Eur,
    Status = Status37.Issued,
    CancellationReason = "cancellation_reason0",
};
```

