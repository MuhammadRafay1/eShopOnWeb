
# Instrument 8

Entity representing instrument.

## Structure

`Instrument8`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Uuid` | `Guid` | Required | Internal instrument identifier. |
| `Isin` | `string` | Optional | International securities identification number defined by [ISO 6166](https://en.wikipedia.org/wiki/International_Securities_Identification_Number).<br><br>**Constraints**: *Pattern*: `^[A-Z]{2}[A-Z0-9]{9}[0-9]$` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Instrument8 instrument8 = new Instrument8
{
    Uuid = new Guid("000010c8-0000-0000-0000-000000000000"),
    Isin = "isin0",
};
```

