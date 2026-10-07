
# Account Liquidation 1

An individual sell order within an account liquidation, corresponding to the sale of a single position held in the account.

## Structure

`AccountLiquidation1`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Universally Unique Identifier (UUID) of an account liquidation order. |
| `Side` | `string` | Required | Side of the order.<br><br>* SELL -<br><br>**Default**: `"SELL"` |
| `Status` | [`Status73`](../../doc/models/status-73.md) | Required | Execution status of the Account liquidation order.<br><br>* NEW -<br>* PROCESSING -<br>* FILLED -<br>* CANCELLED - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

AccountLiquidation1 accountLiquidation1 = new AccountLiquidation1
{
    Id = new Guid("00000d04-0000-0000-0000-000000000000"),
    Side = "SELL",
    Status = Status73.New,
};
```

