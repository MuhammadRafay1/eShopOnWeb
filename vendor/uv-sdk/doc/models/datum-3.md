
# Datum 3

Represents a top-up funding request for an account group. After creation, the end user must send a SEPA Credit Transfer with the provided `settlement_reference` to complete the funding.

## Structure

`Datum3`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Top up request unique identifier. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `AccountGroupId` | `Guid` | Required | Account group unique identifier. |
| `CashAmount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | [`Currency`](../../doc/models/currency.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |
| `SettlementReference` | `string` | Optional | Settlement reference to be included in the corresponding bank transfer to settle this top up.<br><br>**Constraints**: *Maximum Length*: `130`, *Pattern*: `^[0-9A-Za-z+?/\-:()\.,'; ]{0,130}$` |
| `Status` | [`Status29?`](../../doc/models/status-29.md) | Optional | Status of the top up<br><br>* CONFIRMED - The top up is created and confirmed.<br>* SETTLED - The top up is settled.<br>* CANCELLED - The top up is cancelled |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;

Datum3 datum3 = new Datum3
{
    Id = new Guid("00000f70-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    AccountGroupId = new Guid("000021ea-0000-0000-0000-000000000000"),
    CashAmount = "cash_amount0",
    Currency = Currency.Eur,
    SettlementReference = "settlement_reference8",
    Status = Status29.Cancelled,
};
```

