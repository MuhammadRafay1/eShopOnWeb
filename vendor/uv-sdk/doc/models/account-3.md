
# Account 3

*This model accepts additional fields of type object.*

## Structure

`Account3`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Identification` | [`Identification2`](../../doc/models/identification-2.md) | Required | - |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Account3 account3 = new Account3
{
    Identification = new Identification2
    {
        Iban = "iban6",
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

