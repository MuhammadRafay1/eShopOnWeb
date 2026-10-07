
# Order Cancel Response

Response returned when an order cancellation request is accepted. Contains the `id` of the order for which cancellation was requested.

## Structure

`OrderCancelResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Unique identifier for an order. Universally Unique Identifier (UUID). |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

OrderCancelResponse orderCancelResponse = new OrderCancelResponse
{
    Id = new Guid("00000c94-0000-0000-0000-000000000000"),
};
```

