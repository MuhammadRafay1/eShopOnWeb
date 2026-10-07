
# Business Company Update Request

Request payload for updating a company business. Currently only the contact email address can be changed.

*This model accepts additional fields of type object.*

## Structure

`BusinessCompanyUpdateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `ContactEmail` | `string` | Optional | Contact email address of the business. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

BusinessCompanyUpdateRequest businessCompanyUpdateRequest = new BusinessCompanyUpdateRequest
{
    ContactEmail = "contact_email2",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

