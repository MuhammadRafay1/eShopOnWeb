
# Instrument 6

Entity representing the financial instrument.

## Structure

`Instrument6`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Uuid` | `Guid` | Required | String representing the instrument internal identifier. |
| `Isin` | `string` | Optional | International securities identification number defined by [ISO 6166](https://en.wikipedia.org/wiki/International_Securities_Identification_Number).<br><br>**Constraints**: *Pattern*: `^[A-Z]{2}[A-Z0-9]{9}[0-9]$` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Instrument6 instrument6 = new Instrument6
{
    Uuid = new Guid("00001b9a-0000-0000-0000-000000000000"),
    Isin = "isin0",
};
```

