
# Sell to Cover Order Details

## Structure

`SellToCoverOrderDetails`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Sell to cover order id used to cover fee amount. |
| `ResidualAmount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{1,9}(\.[0-9]{2})?$` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

SellToCoverOrderDetails sellToCoverOrderDetails = new SellToCoverOrderDetails
{
    Id = new Guid("00000498-0000-0000-0000-000000000000"),
    ResidualAmount = "residual_amount8",
};
```

