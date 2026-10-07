
# Datum

## Structure

`Datum`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `AccountGroupId` | `Guid` | Required | Account group unique identifier. |
| `Currency` | [`Currency1`](../../doc/models/currency-1.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling<br>* USD - The United States dollar |
| `Balance` | `string` | Required | **Constraints**: *Pattern*: `^-?[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `LockedForTrading` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `PendingSettlement` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `AvailableForWithdrawal` | `string` | Required | **Constraints**: *Pattern*: `^-?[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `AvailableForTrading` | `string` | Required | **Constraints**: *Pattern*: `^-?[0-9]{0,63}(\.[0-9]{1,27})?$` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Datum datum = new Datum
{
    AccountGroupId = new Guid("0000200c-0000-0000-0000-000000000000"),
    Currency = Currency1.Eur,
    Balance = "balance8",
    LockedForTrading = "locked_for_trading2",
    PendingSettlement = "pending_settlement8",
    AvailableForWithdrawal = "available_for_withdrawal6",
    AvailableForTrading = "available_for_trading2",
};
```

