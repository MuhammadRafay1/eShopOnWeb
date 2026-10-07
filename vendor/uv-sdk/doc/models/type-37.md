
# Type 37

Type of the fee collection

* SERVICE_FEE - Service fee intake in a pre-defined cadence (e.g. monthly)
* SERVICE_FEE_LIQUIDATION - Service fee intake as a result of a Portfolio liquidation

## Enumeration

`Type37`

## Fields

| Name |
|  --- |
| `ServiceFee` |
| `ServiceFeeLiquidation` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Type37 type37 = Type37.ServiceFee;
```

