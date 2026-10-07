
# Status 78

Status of the account transfer

* NEW - account transfer is created but not started processing.
* PROCESSING - account transfer is in processing.
* SETTLED - account transfer was successfully settled.
* PARTIALLY_SETTLED - account transfer was partially settled, while some instrument got cancelled.
* CANCELLED - account transfer was cancelled.

## Enumeration

`Status78`

## Fields

| Name |
|  --- |
| `New` |
| `Processing` |
| `Settled` |
| `PartiallySettled` |
| `Cancelled` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Status78 status78 = Status78.Cancelled;
```

