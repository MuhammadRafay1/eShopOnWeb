
# Swift

SWIFT details

## Structure

`Swift`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Iban` | `string` | Required | International Bank Account Number [IBAN](https://en.wikipedia.org/wiki/International_Bank_Account_Number).<br><br>**Constraints**: *Pattern*: `^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,26}$` |
| `Bic` | `string` | Required | Business Identifier Code (also known as SWIFT-BIC, BIC, SWIFT ID or SWIFT code) [ISO 9362](https://en.wikipedia.org/wiki/ISO_9362).<br><br>**Constraints**: *Pattern*: `^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Swift swift = new Swift
{
    Iban = "iban2",
    Bic = "bic0",
};
```

