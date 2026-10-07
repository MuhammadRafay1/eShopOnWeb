
# Datum 5

Represents an incoming SEPA Credit Transfer received by Upvest and credited to an account group. Contains counterparty identification and remittance details from the sender's bank.

## Structure

`Datum5`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Credit Funding request unique identifier |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `AccountGroupId` | `Guid` | Required | Account group unique identifier. |
| `CashAmount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | [`Currency`](../../doc/models/currency.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |
| `Status` | [`Status32?`](../../doc/models/status-32.md) | Optional | Status of the credit funding<br><br>* CONFIRMED - Credit Funding was confirmed.<br>* CANCELLED - Credit Funding was cancelled. |
| `RemittanceInformation` | `string` | Required | Payment reference the one that was used by the end user for the corresponding SEPA Credit Transfer |
| `Counterparty` | [`Counterparty`](../../doc/models/counterparty.md) | Required | - |
| `VirtualBankAccountId` | `Guid?` | Optional | Virtual bank account request unique identifier. |
| `PurposeCode` | `string` | Optional | Purpose of the payment based on *ExternalPurpose1Code* from [ISO 20022](https://www.iso20022.org/catalogue-messages/additional-content-messages/external-code-sets).<br><br>**Constraints**: *Maximum Length*: `4` |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Datum5 datum5 = new Datum5
{
    Id = new Guid("00001ca2-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    AccountGroupId = new Guid("0000080c-0000-0000-0000-000000000000"),
    CashAmount = "cash_amount8",
    Currency = Currency.Eur,
    RemittanceInformation = "remittance_information6",
    Counterparty = new Counterparty
    {
        Identification = new Identification1
        {
            Name = "name2",
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
        Account = new Account3
        {
            Identification = new Identification2
            {
                Iban = "iban6",
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            },
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    Status = Status32.Confirmed,
    VirtualBankAccountId = new Guid("00000618-0000-0000-0000-000000000000"),
    PurposeCode = "purpose_code6",
};
```

