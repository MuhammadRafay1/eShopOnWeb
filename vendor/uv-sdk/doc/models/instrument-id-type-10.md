
# Instrument Id Type 10

The type of the ID used in the request.

* ISIN - International Securities Identification Number
* WKN - German securities identification code

## Enumeration

`InstrumentIdType10`

## Fields

| Name |
|  --- |
| `Isin` |
| `Wkn` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

InstrumentIdType10 instrumentIdType10 = InstrumentIdType10.Isin;
```

