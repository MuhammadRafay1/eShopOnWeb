
# Terms and Conditions 4

Terms and conditions agreement.

*This model accepts additional fields of type object.*

## Structure

`TermsAndConditions4`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `ConsentDocumentId` | `Guid` | Required | UUID of the consent document. |
| `ConfirmedAt` | `DateTime` | Required | Timestamp when the terms and conditions were confirmed. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

TermsAndConditions4 termsAndConditions4 = new TermsAndConditions4
{
    ConsentDocumentId = new Guid("00000f8a-0000-0000-0000-000000000000"),
    ConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

