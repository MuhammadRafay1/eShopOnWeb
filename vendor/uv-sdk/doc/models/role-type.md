
# Role Type

Role type for an account group.

* `OWNER` — The user owns the account group.
* `GUARDIAN` — The user is a legal custodian of a child account group.
* `CHILD` — The user is the child beneficiary of a child account group.

## Enumeration

`RoleType`

## Fields

| Name |
|  --- |
| `Owner` |
| `Guardian` |
| `Child` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

RoleType roleType = RoleType.Owner;
```

