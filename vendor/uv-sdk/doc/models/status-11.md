
# Status 11

Final status of the US Withholding Tax Status check.

* IN_PROGRESS - US Withholding Tax Status check is in progress
* PASSED - US Withholding Tax Status check passed
* FAILED - US Withholding Tax Status check failed

A documented status ("KYC", "W8-BEN" or "W9") is corroborated against the user's tax residency: it fails when the declared `person_type` contradicts the tax residency Upvest holds for the user, or when the user has no active tax residency on record. An "UNDOCUMENTED" status is recorded as declared and is not corroborated against tax residency.

## Enumeration

`Status11`

## Fields

| Name |
|  --- |
| `InProgress` |
| `Passed` |
| `Failed` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Status11 status11 = Status11.Failed;
```

