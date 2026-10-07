
# Direct Debit Mandate

Represents a SEPA Direct Debit mandate authorising Upvest to debit funds from the end user's registered bank account. Only `RECURRENT` mandates are currently supported.

## Structure

`DirectDebitMandate`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Direct Debit Mandate unique identifier. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UserId` | `Guid?` | Optional | Unique identifier of the user, as a UUID. |
| `BusinessId` | `Guid?` | Optional | Unique identifier for the business. |
| `Iban` | `string` | Required | Obfuscated International Bank Account Number [IBAN](https://en.wikipedia.org/wiki/International_Bank_Account_Number).<br><br>**Constraints**: *Pattern*: `^[A-Z]{2}[0-9]{2}[A-Z0-9]{2}\*{10}[A-Z0-9]{4}$` |
| `Bic` | `string` | Required | Business Identifier Code (also known as SWIFT-BIC, BIC, SWIFT ID or SWIFT code) [ISO 9362](https://en.wikipedia.org/wiki/ISO_9362).<br><br>**Constraints**: *Pattern*: `^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$` |
| `CreditorName` | `string` | Required | Name of the creditor on the mandate.<br><br>**Constraints**: *Maximum Length*: `100` |
| `CreditorId` | `string` | Required | Banking identifier of the creditor.<br><br>**Constraints**: *Maximum Length*: `20` |
| `CreditorAddress` | [`Address`](../../doc/models/address.md) | Required | Address. Must not be a P.O. box or c/o address. |
| `Type` | `string` | Required, Constant | Type of mandate.<br><br>* RECURRENT -<br><br>**Value**: `"RECURRENT"` |
| `ConfirmedAt` | `DateTime` | Required | Timestamp of when user validated the mandate |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

DirectDebitMandate directDebitMandate = new DirectDebitMandate
{
    Id = new Guid("0000204a-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    Iban = "iban0",
    Bic = "bic8",
    CreditorName = "creditor_name0",
    CreditorId = "creditor_id2",
    CreditorAddress = new Address
    {
        AddressLine1 = "address_line12",
        Postcode = "postcode2",
        Country = Country.Mt,
        City = "city8",
        AddressLine2 = "address_line20",
        State = "state4",
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    Type = "RECURRENT",
    ConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UserId = new Guid("0000004a-0000-0000-0000-000000000000"),
    BusinessId = new Guid("000008f8-0000-0000-0000-000000000000"),
};
```

