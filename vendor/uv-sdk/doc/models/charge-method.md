
# Charge Method

Indicates whether the fee was charged by client or by other methods.

* CHARGED_BY_CLIENT - Charged by client
* COLLECTED_BY_UPVEST - Charged by client and collected by Upvest

## Enumeration

`ChargeMethod`

## Fields

| Name |
|  --- |
| `ChargedByClient` |
| `CollectedByUpvest` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

ChargeMethod chargeMethod = ChargeMethod.ChargedByClient;
```

