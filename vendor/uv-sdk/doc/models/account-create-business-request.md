
# Account Create Business Request

*This model accepts additional fields of type object.*

## Structure

`AccountCreateBusinessRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `BusinessId` | `Guid` | Required | Unique identifier for the business. |
| `AccountGroupId` | `Guid` | Required | Account group unique identifier. |
| `Type` | [`Type16`](../../doc/models/type-16.md) | Required | Account type.<br><br>* TRADING - Orders in accounts of this type are created on a specific instrument basis.<br>* PORTFOLIO - Orders in accounts of this type are created on a portfolio basis and additional portfolio functionality is available. |
| `Name` | `string` | Optional | The name of the account.<br><br>**Constraints**: *Maximum Length*: `100` |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

AccountCreateBusinessRequest accountCreateBusinessRequest = new AccountCreateBusinessRequest
{
    BusinessId = new Guid("0000183a-0000-0000-0000-000000000000"),
    AccountGroupId = new Guid("00000c1a-0000-0000-0000-000000000000"),
    Type = Type16.Trading,
    Name = "name2",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

