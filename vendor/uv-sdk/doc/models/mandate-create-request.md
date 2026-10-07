
# Mandate Create Request

Request body for creating a direct debit mandate. Exactly one of `user_id` or `business_id` must be provided.

## Structure

`MandateCreateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `UserId` | `Guid?` | Optional | Unique identifier of the user, as a UUID. |
| `BusinessId` | `Guid?` | Optional | Unique identifier for the business. |
| `Iban` | `string` | Required | International Bank Account Number [IBAN](https://en.wikipedia.org/wiki/International_Bank_Account_Number).<br><br>**Constraints**: *Pattern*: `^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,26}$` |
| `Bic` | `string` | Required | Business Identifier Code (also known as SWIFT-BIC, BIC, SWIFT ID or SWIFT code) [ISO 9362](https://en.wikipedia.org/wiki/ISO_9362).<br><br>**Constraints**: *Pattern*: `^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$` |
| `ConfirmedAt` | `DateTime` | Required | Timestamp of when user validated the mandate |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;

MandateCreateRequest mandateCreateRequest = new MandateCreateRequest
{
    Iban = "iban8",
    Bic = "bic6",
    ConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UserId = new Guid("00001056-0000-0000-0000-000000000000"),
    BusinessId = new Guid("00001904-0000-0000-0000-000000000000"),
};
```

