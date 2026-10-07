
# Status 26

Status of the direct debit

* NEW - Direct debit is created but not started processing.
* PROCESSING - Direct debit is in processing.
* CONFIRMED - Direct debit was successfully processed.
* CANCELLED - Direct debit was cancelled.

## Enumeration

`Status26`

## Fields

| Name |
|  --- |
| `New` |
| `Processing` |
| `Confirmed` |
| `Cancelled` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Status26 status26 = Status26.Confirmed;
```

