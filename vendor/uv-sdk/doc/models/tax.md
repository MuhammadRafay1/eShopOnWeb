
# Tax

Tax deducted as part of a payment (e.g. a withdrawal). Contains the tax type and the deducted amount in the specified currency.

*This model accepts additional fields of type object.*

## Structure

`Tax`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Amount` | `string` | Required | **Constraints**: *Pattern*: `^-?[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `Currency` | [`Currency`](../../doc/models/currency.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |
| `Type` | `string` | Required, Constant | Type of the tax.<br><br>* TOTAL - Total taxes<br><br>**Value**: `"TOTAL"` |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Tax tax = new Tax
{
    Amount = "amount6",
    Currency = Currency.Eur,
    Type = "TOTAL",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

