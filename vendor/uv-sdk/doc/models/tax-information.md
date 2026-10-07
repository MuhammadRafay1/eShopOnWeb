
# Tax Information

Tax information of the business.

*This model accepts additional fields of type object.*

## Structure

`TaxInformation`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `TaxCountryCode` | [`TaxCountryCode`](../../doc/models/tax-country-code.md) | Required | Accepted country code. [ISO 3166-1 alpha-2 codes](https://en.wikipedia.org/wiki/ISO_3166-1_alpha-2). |
| `TaxIdentificationType` | [`TaxIdentificationType`](../../doc/models/tax-identification-type.md) | Required | Type of tax identification used by the business. |
| `TaxIdentificationNumber` | `string` | Required | Tax identification number of the business.<br><br>When `tax_identification_type` is `STEUERNUMMER`, the value is 10 or 11 digits, provided either as bare digits or in a state-specific slash-grouped layout. Slashes are all-or-nothing and must appear at the grouping positions:<br><br>\| Format \| Grouping \| Example \|<br>\| --- \| --- \| --- \|<br>\| Bare digits \| 10 or 11 digits \| `1234567890`, `12345678901` \|<br>\| North Rhine-Westphalia \| 3/4/4 \| `133/8150/8159` \|<br>\| Bavaria \| 3/3/5 \| `181/815/08155` \|<br>\| Standard 10-digit \| 2/3/5 \| `24/815/08151` \|<br>\| Alternative 10-digit (Berlin, Bremen, Lower Saxony) \| 3/3/4 \| `181/815/0815` \|<br><br>When `tax_identification_type` is `WIRTSCHAFTSIDENTIFIKATIONSNUMMER`, the value is `DE` followed by 9 digits, with an optional 5-digit distinguishing feature (for example `DE123456789` or `DE123456789-00001`). |
| `IsResidentInMultipleTaxJurisdictions` | `bool` | Required | Indicates if the business is resident in multiple tax jurisdictions. |
| `IsSubjectToFatca` | `bool` | Required | Indicates if the business is subject to FATCA regulations. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

TaxInformation taxInformation = new TaxInformation
{
    TaxCountryCode = TaxCountryCode.Pf,
    TaxIdentificationType = TaxIdentificationType.Steuernummer,
    TaxIdentificationNumber = "tax_identification_number8",
    IsResidentInMultipleTaxJurisdictions = false,
    IsSubjectToFatca = false,
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

