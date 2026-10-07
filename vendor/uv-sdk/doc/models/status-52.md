
# Status 52

Status of the execution.

* FILLED — the execution has been filled.
* SETTLED — the execution has settled and securities and cash have been exchanged.
* CANCELLED — the execution was cancelled before settlement.

## Enumeration

`Status52`

## Fields

| Name |
|  --- |
| `Filled` |
| `Settled` |
| `Cancelled` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Status52 status52 = Status52.Cancelled;
```

