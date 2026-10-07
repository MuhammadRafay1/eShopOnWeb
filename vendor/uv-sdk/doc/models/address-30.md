
# Address 30

User residential address.

*This model accepts additional fields of type object.*

## Structure

`Address30`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `AddressLine1` | `string` | Optional | First line of the address.<br><br>**Constraints**: *Maximum Length*: `100` |
| `AddressLine2` | `string` | Optional | Second line of the address.<br><br>**Constraints**: *Maximum Length*: `100` |
| `Postcode` | `string` | Optional | Postal code (postcode, PIN or ZIP code)<br><br>**Constraints**: *Pattern*: `^[a-zA-Z0-9][a-zA-Z0-9\s\-]{0,8}[a-zA-Z0-9]?$` |
| `Country` | `string` | Optional | Country code. [ISO 3166 alpha-2 Codes](https://en.wikipedia.org/wiki/ISO_3166-1_alpha-2).<br><br>**Constraints**: *Pattern*: `^[A-Z]{2}$` |
| `State` | `string` | Optional | State, province, county. [ISO 3166 alpha-2 Codes](https://en.wikipedia.org/wiki/ISO_3166-1_alpha-2).<br><br>**Constraints**: *Maximum Length*: `50` |
| `City` | `string` | Optional | **Constraints**: *Minimum Length*: `1`, *Maximum Length*: `85` |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Address30 address30 = new Address30
{
    AddressLine1 = "address_line14",
    AddressLine2 = "address_line22",
    Postcode = "postcode4",
    Country = "country4",
    State = "state6",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

