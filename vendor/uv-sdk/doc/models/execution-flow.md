
# Execution Flow

Execution flow for order processing. Defaults to `STRAIGHT_THROUGH` if not specified.

* STRAIGHT_THROUGH — the order is routed and executed directly without manual intervention.
* BLOCK — the order is bundled with other orders for block execution.

## Enumeration

`ExecutionFlow`

## Fields

| Name |
|  --- |
| `StraightThrough` |
| `Block` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

ExecutionFlow executionFlow = ExecutionFlow.StraightThrough;
```

