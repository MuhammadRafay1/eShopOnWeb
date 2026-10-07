
# Transaction Fee Model

A fee configuration that references a predefined transaction fee model by its ID, rather than specifying a fee amount directly.

*This model accepts additional fields of type object.*

## Structure

`TransactionFeeModel`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Type` | [`FeeType`](../../doc/models/fee-type.md) | Required | Fee type<br><br>* TRANSACTION_FEE_BUY -<br>* TRANSACTION_FEE_SELL - |
| `TransactionFeeModelId` | `Guid` | Required | The ID of the transaction fee model. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

TransactionFeeModel transactionFeeModel = new TransactionFeeModel
{
    Type = FeeType.TransactionFeeBuy,
    TransactionFeeModelId = new Guid("00000e7a-0000-0000-0000-000000000000"),
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

