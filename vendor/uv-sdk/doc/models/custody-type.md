
# Custody Type

Custody type for child account groups.

* `SOLE_CUSTODY` — A single guardian has custody of the child account group.
* `JOINT_CUSTODY` — Multiple guardians are required for the child account group.

## Enumeration

`CustodyType`

## Fields

| Name |
|  --- |
| `SoleCustody` |
| `JointCustody` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

CustodyType custodyType = CustodyType.SoleCustody;
```

