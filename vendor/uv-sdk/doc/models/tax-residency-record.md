
# Tax Residency Record

A set of tax residencies submitted for a user, with the processing status and timestamps. The record is `PENDING` until processed by Upvest, then `ACTIVE` when in use.

## Structure

`TaxResidencyRecord`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `TaxResidencies` | [`List<TaxResidencyRecordTaxResidencies>`](../../doc/models/containers/tax-residency-record-tax-residencies.md) | Required | This is List of a container for one-of cases. |
| `Status` | [`Status63`](../../doc/models/status-63.md) | Required | Tax residency status<br><br>* PENDING - It indicates that the tax residency records are not yet processed by Upvest.<br>* ACTIVE - It indicates that tax residency records are processed, and the tax residency record is the one in use. |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Models.Containers;
using UpvestInvestmentApi.Standard.Utilities;

TaxResidencyRecord taxResidencyRecord = new TaxResidencyRecord
{
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    TaxResidencies = new List<TaxResidencyRecordTaxResidencies>
    {
        TaxResidencyRecordTaxResidencies.FromWithTaxIdentifierNumber(
            new WithTaxIdentifierNumber
            {
                Country = Country.Mr,
                TaxIdentifierNumber = "tax_identifier_number6",
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            }
        ),
        TaxResidencyRecordTaxResidencies.FromWithTaxIdentifierNumber(
            new WithTaxIdentifierNumber
            {
                Country = Country.Mr,
                TaxIdentifierNumber = "tax_identifier_number6",
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            }
        ),
    },
    Status = Status63.Pending,
};
```

