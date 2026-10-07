
# Reference Account

A verified reference bank account belonging to a user, used as the destination for cash withdrawals.

## Structure

`ReferenceAccount`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Reference account unique identifier. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UserId` | `Guid?` | Optional | Unique identifier of the user, as a UUID. |
| `BusinessId` | `Guid?` | Optional | Unique identifier for the business. |
| `AccountOwner` | `string` | Required | Name of the reference account holder<br><br>**Constraints**: *Maximum Length*: `140` |
| `Name` | `string` | Required | Human-readable name of the reference bank account<br><br>**Constraints**: *Maximum Length*: `100` |
| `Iban` | `string` | Required | Obfuscated International Bank Account Number [IBAN](https://en.wikipedia.org/wiki/International_Bank_Account_Number).<br><br>**Constraints**: *Pattern*: `^[A-Z]{2}[0-9]{2}[A-Z0-9]{2}\*{10}[A-Z0-9]{4}$` |
| `Bic` | `string` | Required | Business Identifier Code (also known as SWIFT-BIC, BIC, SWIFT ID or SWIFT code) [ISO 9362](https://en.wikipedia.org/wiki/ISO_9362).<br><br>**Constraints**: *Pattern*: `^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$` |
| `ConfirmedAt` | `DateTime` | Required | Timestamp of when user validated the reference account |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;

ReferenceAccount referenceAccount = new ReferenceAccount
{
    Id = new Guid("00001d50-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    AccountOwner = "account_owner2",
    Name = "name4",
    Iban = "iban8",
    Bic = "bic6",
    ConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UserId = new Guid("00002460-0000-0000-0000-000000000000"),
    BusinessId = new Guid("000005fe-0000-0000-0000-000000000000"),
};
```

