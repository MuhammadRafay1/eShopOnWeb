
# Settlement Counterparty Account

Account of a settlement counterparty.

* `SAFE` - Safekeeping account.

## Structure

`SettlementCounterpartyAccount`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Type` | `string` | Required, Constant | Type of the account.<br><br>* `SAFE` - Safekeeping account.<br><br>**Value**: `"SAFE"` |
| `MValue` | `string` | Required | Account identifier value. |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

SettlementCounterpartyAccount settlementCounterpartyAccount = new SettlementCounterpartyAccount
{
    Type = "SAFE",
    MValue = "value6",
};
```

