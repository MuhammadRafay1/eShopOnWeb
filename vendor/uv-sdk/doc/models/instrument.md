
# Instrument

## Structure

`Instrument`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Uuid` | `Guid` | Required | Instrument unique identifier. |
| `Isin` | `string` | Optional | International securities identification number defined by [ISO 6166](https://en.wikipedia.org/wiki/International_Securities_Identification_Number).<br><br>**Constraints**: *Pattern*: `^[A-Z]{2}[A-Z0-9]{9}[0-9]$` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Instrument instrument = new Instrument
{
    Uuid = new Guid("00001fd8-0000-0000-0000-000000000000"),
    Isin = "isin4",
};
```

