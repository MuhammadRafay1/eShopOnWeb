
# Account Create User Request

*This model accepts additional fields of type object.*

## Structure

`AccountCreateUserRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `UserId` | `Guid` | Required | Unique identifier of the user, as a UUID. |
| `AccountGroupId` | `Guid` | Required | Account group unique identifier. |
| `Type` | [`Type16`](../../doc/models/type-16.md) | Required | Account type.<br><br>* TRADING - Orders in accounts of this type are created on a specific instrument basis.<br>* PORTFOLIO - Orders in accounts of this type are created on a portfolio basis and additional portfolio functionality is available. |
| `Name` | `string` | Optional | The name of the account.<br><br>**Constraints**: *Maximum Length*: `100` |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

AccountCreateUserRequest accountCreateUserRequest = new AccountCreateUserRequest
{
    UserId = new Guid("00000cbe-0000-0000-0000-000000000000"),
    AccountGroupId = new Guid("00001828-0000-0000-0000-000000000000"),
    Type = Type16.Trading,
    Name = "name4",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

