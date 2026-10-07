
# Identification

Identification details

## Structure

`Identification`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Swift` | [`Swift`](../../doc/models/swift.md) | Optional | SWIFT details |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Identification identification = new Identification
{
    Swift = new Swift
    {
        Iban = "iban2",
        Bic = "bic0",
    },
};
```

