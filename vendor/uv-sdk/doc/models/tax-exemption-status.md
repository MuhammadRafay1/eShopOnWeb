
# Tax Exemption Status

Tax exemption status

* NEW - The tax exemption request is created.
* ACTIVE - The tax exemption is valid and active for the current year.
* EXPIRED - The tax exemption is no longer `ACTIVE` and the `valid_to_date` already lies in the past. An update is not possible.
* CANCELLED - The tax exemption could not be created or was cancelled.

## Enumeration

`TaxExemptionStatus`

## Fields

| Name |
|  --- |
| `New` |
| `Active` |
| `Expired` |
| `Cancelled` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

TaxExemptionStatus taxExemptionStatus = TaxExemptionStatus.New;
```

