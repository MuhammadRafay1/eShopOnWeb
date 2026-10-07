
# Value Type

The value type of the transaction fee model.

* `ABSOLUTE` — Tier fees are fixed cash amounts.
* `RELATIVE` — Tier fees are percentages of the order value, expressed in basis points.

## Enumeration

`ValueType`

## Fields

| Name |
|  --- |
| `Absolute` |
| `Relative` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

ValueType valueType = ValueType.Absolute;
```

