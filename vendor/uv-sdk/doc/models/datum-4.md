
# Datum 4

## Structure

`Datum4`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `VirtualBankAccountId` | `Guid?` | Optional | Virtual bank account request unique identifier. |
| `Name` | `string` | Optional | Name of the recipient account for the SEPA Credit Transfer |
| `Currency` | [`Currency`](../../doc/models/currency.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |
| `Owner` | [`Owner`](../../doc/models/owner.md) | Required | Owner of the virtual bank account |
| `Identification` | [`Identification`](../../doc/models/identification.md) | Required | Identification details |
| `RemittanceInformation` | `string` | Optional | Payment reference the one that should be used for SEPA Credit Transfer |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Datum4 datum4 = new Datum4
{
    Currency = Currency.Eur,
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
    VirtualBankAccountId = new Guid("0000075c-0000-0000-0000-000000000000"),
    Name = "name4",
    RemittanceInformation = "remittance_information0",
};
```

