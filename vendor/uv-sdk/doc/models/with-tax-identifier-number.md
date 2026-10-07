
# With Tax Identifier Number

*This model accepts additional fields of type object.*

## Structure

`WithTaxIdentifierNumber`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Country` | [`Country`](../../doc/models/country.md) | Required | Accepted country code. [ISO 3166-1 alpha-2 codes](https://en.wikipedia.org/wiki/ISO_3166-1_alpha-2). |
| `TaxIdentifierNumber` | `string` | Required | Tax identification number (TIN) issued to the user by the tax authorities of the given country<br><br>**Constraints**: *Minimum Length*: `1`, *Maximum Length*: `20` |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

WithTaxIdentifierNumber withTaxIdentifierNumber = new WithTaxIdentifierNumber
{
    Country = Country.Kp,
    TaxIdentifierNumber = "tax_identifier_number6",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

