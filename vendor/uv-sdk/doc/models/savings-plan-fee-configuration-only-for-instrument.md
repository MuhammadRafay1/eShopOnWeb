
# Savings Plan Fee Configuration Only for Instrument

## Structure

`SavingsPlanFeeConfigurationOnlyForInstrument`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Type` | `string` | Required, Constant | Fee type<br><br>* TRANSACTION_FEE_BUY -<br><br>**Value**: `"TRANSACTION_FEE_BUY"` |
| `TransactionFeeModelId` | `Guid` | Required | Universally Unique Identifier (UUID) of the transaction fee model. |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

SavingsPlanFeeConfigurationOnlyForInstrument savingsPlanFeeConfigurationOnlyForInstrument = new SavingsPlanFeeConfigurationOnlyForInstrument
{
    Type = "TRANSACTION_FEE_BUY",
    TransactionFeeModelId = new Guid("00001888-0000-0000-0000-000000000000"),
};
```

