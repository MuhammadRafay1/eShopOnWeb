
# Identification 9

Identification information of the sole trader business.

*This model accepts additional fields of type object.*

## Structure

`Identification9`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `CompanyName` | `string` | Required | Name of the sole trader business. |
| `IncorporationDate` | `string` | Optional | Incorporation date of the business in YYYY-MM-DD format. [RFC 3339, section 5.6](https://json-schema.org/draft/2020-12/json-schema-validation.html#RFC3339) |
| `LegalDesignation` | [`LegalDesignation1`](../../doc/models/legal-designation-1.md) | Required | Country specific legal designation of the sole trader business. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Identification9 identification9 = new Identification9
{
    CompanyName = "company_name6",
    LegalDesignation = LegalDesignation1.Einzelunternehmer,
    IncorporationDate = "incorporation_date6",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

