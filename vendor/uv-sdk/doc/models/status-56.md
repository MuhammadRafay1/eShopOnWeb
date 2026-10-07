
# Status 56

Status of the tax wrapper

* NEW - The tax wrapper is newly created and not yet active due to pending checks.
* ACTIVE - The tax wrapper is currently active and in use.
* INACTIVE - The tax wrapper is not currently active. New subscriptions not allowed.
* CLOSED - The tax wrapper has been closed and is no longer available.

## Enumeration

`Status56`

## Fields

| Name |
|  --- |
| `New` |
| `Active` |
| `Inactive` |
| `Closed` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Status56 status56 = Status56.New;
```

