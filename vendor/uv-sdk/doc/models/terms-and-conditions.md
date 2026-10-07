
# Terms and Conditions

Terms & Conditions will be needed for all users unless they are a child user or only a user on a business.

*This model accepts additional fields of type object.*

## Structure

`TermsAndConditions`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `ConsentDocumentId` | `Guid` | Required | Consent unique identifier. |
| `ConfirmedAt` | `DateTime` | Required | Timestamp at which the user consented to the terms & conditions. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

TermsAndConditions termsAndConditions = new TermsAndConditions
{
    ConsentDocumentId = new Guid("000026c0-0000-0000-0000-000000000000"),
    ConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

