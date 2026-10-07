
# Address

Address. Must not be a P.O. box or c/o address.

*This model accepts additional fields of type object.*

## Structure

`Address`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `AddressLine1` | `string` | Required | First address line of the address.<br><br>**Constraints**: *Maximum Length*: `100` |
| `AddressLine2` | `string` | Optional | Second address line of the address.<br><br>**Constraints**: *Maximum Length*: `100` |
| `Postcode` | `string` | Required | Postal code (postcode, PIN or ZIP code)<br><br>**Constraints**: *Pattern*: `^[a-zA-Z0-9][a-zA-Z0-9\s\-]{0,8}[a-zA-Z0-9]?$` |
| `Country` | [`Country`](../../doc/models/country.md) | Required | Accepted country code. [ISO 3166-1 alpha-2 codes](https://en.wikipedia.org/wiki/ISO_3166-1_alpha-2). |
| `State` | `string` | Optional | State, province, county. [ISO 3166 alpha-2 Codes](https://en.wikipedia.org/wiki/ISO_3166-1_alpha-2).<br><br>**Constraints**: *Maximum Length*: `50` |
| `City` | `string` | Required | **Constraints**: *Minimum Length*: `1`, *Maximum Length*: `85` |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Address address = new Address
{
    AddressLine1 = "address_line10",
    Postcode = "postcode0",
    Country = Country.Bf,
    City = "city6",
    AddressLine2 = "address_line28",
    State = "state2",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

