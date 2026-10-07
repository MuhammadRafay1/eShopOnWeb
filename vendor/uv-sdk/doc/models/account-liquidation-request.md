
# Account Liquidation Request

*This model accepts additional fields of type object.*

## Structure

`AccountLiquidationRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `UserId` | `Guid` | Required | Unique identifier of the user, as a UUID. |
| `BusinessId` | `Guid?` | Optional | Unique identifier for the business. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

AccountLiquidationRequest accountLiquidationRequest = new AccountLiquidationRequest
{
    UserId = new Guid("00001e10-0000-0000-0000-000000000000"),
    BusinessId = new Guid("000026be-0000-0000-0000-000000000000"),
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

