
# Status 51

The execution status of the order.

* NEW — the order has been received and validated, awaiting routing.
* PROCESSING — the order is being routed for execution.
* FILLED — the order has been fully executed.
* CANCELLED — the order was cancelled before being fully executed.

## Enumeration

`Status51`

## Fields

| Name |
|  --- |
| `New` |
| `Processing` |
| `Filled` |
| `Cancelled` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Status51 status51 = Status51.Filled;
```

