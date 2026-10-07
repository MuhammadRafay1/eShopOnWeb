
# Status 79

Status of the securities transfer

* `NEW` - Securities transfer is created but not started processing.
* `PROCESSING` - Securities transfer is in processing.
* `SETTLED` - Securities transfer was successfully settled.
* `CANCELLED` - Securities transfer was cancelled.

## Enumeration

`Status79`

## Fields

| Name |
|  --- |
| `New` |
| `Processing` |
| `Settled` |
| `Cancelled` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Status79 status79 = Status79.New;
```

