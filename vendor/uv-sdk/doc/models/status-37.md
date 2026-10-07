
# Status 37

Status of the cash balance transfer.

* ISSUED - Transfer has been created and the cash movement is in progress.
* CONFIRMED - Cash was successfully moved from the source to the target account group.
* CANCELLED - Transfer was cancelled and no cash was moved.

## Enumeration

`Status37`

## Fields

| Name |
|  --- |
| `Issued` |
| `Confirmed` |
| `Cancelled` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Status37 status37 = Status37.Confirmed;
```

