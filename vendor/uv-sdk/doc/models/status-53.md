
# Status 53

Status of the fee collection

* PROCESSING - Fee collection is in progress.
* FINALISED - Fees have been collected from the account and the funds has been transferred to the client.
* CANCELLED - Fee collection has been cancelled.

## Enumeration

`Status53`

## Fields

| Name |
|  --- |
| `Processing` |
| `Finalised` |
| `Cancelled` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Status53 status53 = Status53.Processing;
```

