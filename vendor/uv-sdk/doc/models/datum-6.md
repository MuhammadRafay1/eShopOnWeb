
# Datum 6

Represents a virtual bank account assigned to an account group. Provides an IBAN and BIC that end users can use to send SEPA Credit Transfers directly to their investment account.

## Structure

`Datum6`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Virtual bank account request unique identifier. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `AccountGroupId` | `Guid` | Required | Account group unique identifier. |
| `Name` | `string` | Optional | Name of the virtual bank account |
| `Owner` | [`Owner`](../../doc/models/owner.md) | Required | Owner of the virtual bank account |
| `Identification` | [`Identification`](../../doc/models/identification.md) | Required | Identification details |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;

Datum6 datum6 = new Datum6
{
    Id = new Guid("00000054-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    AccountGroupId = new Guid("00001442-0000-0000-0000-000000000000"),
    Owner = new Owner
    {
        Name = "name4",
    },
    Identification = new Identification
    {
        Swift = new Swift
        {
            Iban = "iban2",
            Bic = "bic0",
        },
    },
    Name = "name4",
};
```

