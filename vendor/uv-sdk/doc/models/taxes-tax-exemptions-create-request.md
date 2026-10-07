
# Taxes Tax Exemptions Create Request

## Structure

`TaxesTaxExemptionsCreateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `UserIds` | `List<Guid>` | Required | Ids of the users for whom the tax exemption is to be created. |
| `TaxExemptionDetails` | [`TaxExemptionRequestGermanTaxExemptionDetails`](../../doc/models/tax-exemption-request-german-tax-exemption-details.md) | Required | - |
| `Country` | `string` | Required | Country code. [ISO 3166 alpha-2 Codes](https://en.wikipedia.org/wiki/ISO_3166-1_alpha-2).<br><br>**Constraints**: *Pattern*: `^[A-Z]{2}$` |
| `ValidToDate` | `DateTime?` | Optional | Date until which the tax exemption is valid. If it is unlimited, it is omitted. For Germany it is always the last day of the year (YYYY-12-31). [RFC 3339, section 5.6](https://json-schema.org/draft/2020-12/json-schema-validation.html#RFC3339) |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;

TaxesTaxExemptionsCreateRequest taxesTaxExemptionsCreateRequest = new TaxesTaxExemptionsCreateRequest
{
    UserIds = new List<Guid>
    {
        new Guid("00000545-0000-0000-0000-000000000000"),
        new Guid("00000546-0000-0000-0000-000000000000"),
        new Guid("00000547-0000-0000-0000-000000000000"),
    },
    TaxExemptionDetails = new TaxExemptionRequestGermanTaxExemptionDetails
    {
        TaxExemptionAmount = new TaxExemptionCreateRequestTaxExemptionDetailsAmount
        {
            Amount = "amount4",
            Currency = Currency.Eur,
        },
        TaxExemptionType = TaxExemptionType.Single,
    },
    Country = "country2",
    ValidToDate = DateTime.Parse("2016-03-13"),
};
```

