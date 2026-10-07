
# Instrument Id Type 4

The type of the ID used in the request.

* ISIN - International Securities Identification Number
* UPVEST - UPVEST's unique instrument identifier

## Enumeration

`InstrumentIdType4`

## Fields

| Name |
|  --- |
| `Isin` |
| `Upvest` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

InstrumentIdType4 instrumentIdType4 = InstrumentIdType4.Isin;
```

