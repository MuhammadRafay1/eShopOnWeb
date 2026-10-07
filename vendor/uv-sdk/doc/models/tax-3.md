
# Tax 3

Tax deducted as part of an order execution. Contains the tax type and the deducted amount.

*This model accepts additional fields of type object.*

## Structure

`Tax3`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Type` | `string` | Required, Constant | Tax type<br><br>* TOTAL -<br><br>**Value**: `"TOTAL"` |
| `Amount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Tax3 tax3 = new Tax3
{
    Type = "TOTAL",
    Amount = "amount4",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

