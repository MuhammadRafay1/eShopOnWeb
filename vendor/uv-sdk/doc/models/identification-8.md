
# Identification 8

Identification information of the business.

*This model accepts additional fields of type object.*

## Structure

`Identification8`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `CompanyName` | `string` | Required | Name of the company. |
| `IncorporationDate` | `string` | Required | Incorporation date of the business in YYYY-MM-DD format. [RFC 3339, section 5.6](https://json-schema.org/draft/2020-12/json-schema-validation.html#RFC3339) |
| `DistrictCourt` | `string` | Optional | District court where the company is registered.<br><br>**Constraints**: *Minimum Length*: `1`, *Maximum Length*: `40` |
| `LegalDesignation` | [`LegalDesignation`](../../doc/models/legal-designation.md) | Required | Country specific legal designation of the company. |
| `LegalEntityIdentifier` | `string` | Optional | Legal Entity Identifier (LEI) of the company. A 20-character alphanumeric code as defined by [ISO 17442](https://en.wikipedia.org/wiki/Legal_Entity_Identifier).<br><br>**Constraints**: *Pattern*: `^[A-Z0-9]{18}[0-9]{2}$` |
| `CommonReportingStandards` | [`CommonReportingStandards`](../../doc/models/common-reporting-standards.md) | Required | Common reporting standards information. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Identification8 identification8 = new Identification8
{
    CompanyName = "company_name4",
    IncorporationDate = "incorporation_date4",
    LegalDesignation = LegalDesignation.GesellschaftMitBeschraenkterHaftung,
    CommonReportingStandards = new CommonReportingStandards
    {
        IsPassiveNonFinancialEntity = false,
        IsFinancialInstitution = false,
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    DistrictCourt = "district_court8",
    LegalEntityIdentifier = "legal_entity_identifier0",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

