
# Webhook Tax Exemption Created Tax Exemption

## Structure

`WebhookTaxExemptionCreatedTaxExemption`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Tax Exemption Unique Identifier |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `Status` | [`TaxExemptionStatus`](../../doc/models/tax-exemption-status.md) | Required | Tax exemption status<br><br>* NEW - The tax exemption request is created.<br>* ACTIVE - The tax exemption is valid and active for the current year.<br>* EXPIRED - The tax exemption is no longer `ACTIVE` and the `valid_to_date` already lies in the past. An update is not possible.<br>* CANCELLED - The tax exemption could not be created or was cancelled. |
| `UserIds` | `List<Guid>` | Required | Ids of the users for whom the tax exemption was created. |
| `Country` | `string` | Required | Country code. [ISO 3166 alpha-2 Codes](https://en.wikipedia.org/wiki/ISO_3166-1_alpha-2).<br><br>**Constraints**: *Pattern*: `^[A-Z]{2}$` |
| `ValidFromDate` | `DateTime` | Required | Date from which the tax exemption is valid. [RFC 3339, section 5.6](https://json-schema.org/draft/2020-12/json-schema-validation.html#RFC3339) |
| `ValidToDate` | `DateTime?` | Required | Date until which the tax exemption is valid. If it is unlimited, it is omitted. For Germany it is always the last day of the year (YYYY-12-31). [RFC 3339, section 5.6](https://json-schema.org/draft/2020-12/json-schema-validation.html#RFC3339) |
| `TaxExemptionDetails` | [`TaxExemptionGermanTaxExemptionDetails`](../../doc/models/tax-exemption-german-tax-exemption-details.md) | Required | - |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;

WebhookTaxExemptionCreatedTaxExemption webhookTaxExemptionCreatedTaxExemption = new WebhookTaxExemptionCreatedTaxExemption
{
    Id = new Guid("00002476-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    Status = TaxExemptionStatus.Expired,
    UserIds = new List<Guid>
    {
        new Guid("000007df-0000-0000-0000-000000000000"),
        new Guid("000007e0-0000-0000-0000-000000000000"),
    },
    Country = "country8",
    ValidFromDate = DateTime.Parse("2016-03-13"),
    ValidToDate = DateTime.Parse("2016-03-13"),
    TaxExemptionDetails = new TaxExemptionGermanTaxExemptionDetails
    {
        TaxExemptionType = TaxExemptionType.Single,
        TaxExemptionAmount = new TaxExemptionCreateRequestTaxExemptionDetailsAmount
        {
            Amount = "amount4",
            Currency = Currency.Eur,
        },
        UtilizedAmount = new TaxExemptionCreateRequestTaxExemptionDetailsAmount
        {
            Amount = "amount0",
            Currency = Currency.Eur,
        },
        RemainingAmount = new TaxExemptionCreateRequestTaxExemptionDetailsAmount
        {
            Amount = "amount8",
            Currency = Currency.Eur,
        },
    },
};
```

