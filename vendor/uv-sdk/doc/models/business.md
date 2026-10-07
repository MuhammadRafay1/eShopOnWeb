
# Business

Business details.

*This model accepts additional fields of type object.*

## Structure

`Business`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `CompanyName` | `string` | Optional | Name of the company. |
| `Address` | [`Address`](../../doc/models/address.md) | Optional | Address. Must not be a P.O. box or c/o address. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Business business = new Business
{
    CompanyName = "company_name8",
    Address = new Address
    {
        AddressLine1 = "address_line10",
        Postcode = "postcode0",
        Country = Country.Bf,
        City = "city6",
        AddressLine2 = "address_line28",
        State = "state2",
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

