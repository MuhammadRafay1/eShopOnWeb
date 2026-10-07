
# Without Tax Identifier Number

*This model accepts additional fields of type object.*

## Structure

`WithoutTaxIdentifierNumber`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Country` | [`Country`](../../doc/models/country.md) | Required | Accepted country code. [ISO 3166-1 alpha-2 codes](https://en.wikipedia.org/wiki/ISO_3166-1_alpha-2). |
| `MissingTinReason` | [`MissingTinReason`](../../doc/models/missing-tin-reason.md) | Required | Reason why TIN is missing<br><br>* TIN_NOT_YET_ASSIGNED - Indicates that the tax identification number has not yet been assigned by the tax authorities. A common example is, that a user has moved to a country and thus became taxable, but that the tax authorities have not yet assigned the TIN to this user.<br>* COUNTRY_HAS_NO_TIN - Indicates that the specific country does not provide a TIN.<br>* OTHER_REASONS - Applies in case of other reasons - i.e. when a user does not have the TIN at hand. Note this may cause additional inquiries by our customer service team. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

WithoutTaxIdentifierNumber withoutTaxIdentifierNumber = new WithoutTaxIdentifierNumber
{
    Country = Country.Bo,
    MissingTinReason = MissingTinReason.CountryHasNoTin,
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

