
# Account Liquidation Request 1

*This model accepts additional fields of type object.*

## Structure

`AccountLiquidationRequest1`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `UserId` | `Guid?` | Optional | Unique identifier of the user, as a UUID. |
| `BusinessId` | `Guid` | Required | Unique identifier for the business. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

AccountLiquidationRequest1 accountLiquidationRequest1 = new AccountLiquidationRequest1
{
    BusinessId = new Guid("00001540-0000-0000-0000-000000000000"),
    UserId = new Guid("00000c92-0000-0000-0000-000000000000"),
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

