
# Identification 2

*This model accepts additional fields of type object.*

## Structure

`Identification2`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Iban` | `string` | Required | International Bank Account Number [IBAN](https://en.wikipedia.org/wiki/International_Bank_Account_Number).<br><br>**Constraints**: *Pattern*: `^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,26}$` |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Identification2 identification2 = new Identification2
{
    Iban = "iban4",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

