
# Security Transaction Reference

Entity representing security transaction reference.

## Structure

`SecurityTransactionReference`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Unique identifier for a resource of given type. |
| `Type` | [`Type51`](../../doc/models/type-51.md) | Required | Type of the reference.<br><br>* ORDER - Order<br>* ORDER_EXECUTION - Order execution<br>* CORPORATE_ACTION - Corporate action<br>* CORPORATE_ACTION_TRANSACTION_ID - Corporate action transaction ID |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

SecurityTransactionReference securityTransactionReference = new SecurityTransactionReference
{
    Id = new Guid("0000106e-0000-0000-0000-000000000000"),
    Type = Type51.Order,
};
```

