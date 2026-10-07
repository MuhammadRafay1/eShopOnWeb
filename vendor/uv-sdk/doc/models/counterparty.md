
# Counterparty

*This model accepts additional fields of type object.*

## Structure

`Counterparty`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Identification` | [`Identification1`](../../doc/models/identification-1.md) | Required | - |
| `Account` | [`Account3`](../../doc/models/account-3.md) | Required | - |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Counterparty counterparty = new Counterparty
{
    Identification = new Identification1
    {
        Name = "name2",
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    Account = new Account3
    {
        Identification = new Identification2
        {
            Iban = "iban6",
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

