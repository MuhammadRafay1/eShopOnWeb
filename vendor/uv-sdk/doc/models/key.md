
# Key

*This model accepts additional fields of type object.*

## Structure

`Key`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Kid` | `Guid?` | Optional | Key ID |
| `Kty` | [`Kty?`](../../doc/models/kty.md) | Optional | Cryptographic algorithm family used with the key.<br><br>* EC -<br><br>**Default**: `Kty.EC` |
| `Crv` | [`Crv?`](../../doc/models/crv.md) | Optional | Elliptic curve family.<br><br>* P-521 -<br><br>**Default**: `Crv.P521` |
| `X` | `string` | Optional | Curve parameter |
| `Y` | `string` | Optional | Curve parameter |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Key key = new Key
{
    Kid = new Guid("00000132-0000-0000-0000-000000000000"),
    Kty = Kty.Ec,
    Crv = Crv.P521,
    X = "x6",
    Y = "y4",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

