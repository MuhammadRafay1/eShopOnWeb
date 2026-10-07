
# Accounts Positions Response

## Structure

`AccountsPositionsResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `AccountId` | `Guid` | Required | Account unique identifier. |
| `Instrument` | [`Instrument`](../../doc/models/instrument.md) | Required | - |
| `Quantity` | `string` | Required | **Constraints**: *Pattern*: `^-?[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `LockedForTrading` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `PendingSettlement` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `AvailableForTrading` | `string` | Required | **Constraints**: *Pattern*: `^-?[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `AvailableForInstruction` | `string` | Required | **Constraints**: *Pattern*: `^-?[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `SettledQuantity` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

AccountsPositionsResponse accountsPositionsResponse = new AccountsPositionsResponse
{
    AccountId = new Guid("00000ea0-0000-0000-0000-000000000000"),
    Instrument = new Instrument
    {
        Uuid = new Guid("00001fd8-0000-0000-0000-000000000000"),
        Isin = "isin4",
    },
    Quantity = "quantity8",
    LockedForTrading = "locked_for_trading6",
    PendingSettlement = "pending_settlement6",
    AvailableForTrading = "available_for_trading0",
    AvailableForInstruction = "available_for_instruction4",
    SettledQuantity = "settled_quantity4",
};
```

