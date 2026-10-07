
# Status 18

Status of the account group

* PENDING_APPROVAL - Account group approval is pending - the account group is visible through our API but cannot be acted on.
* ACTIVE - Account group is active - full functionality of the Investment API is accessible.
* CLOSING - Account group is closing.
* CLOSED - Account group is closed.
* LOCKED - Account group is locked for all actions.

## Enumeration

`Status18`

## Fields

| Name |
|  --- |
| `PendingApproval` |
| `Active` |
| `Closing` |
| `Closed` |
| `Locked` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Status18 status18 = Status18.PendingApproval;
```

