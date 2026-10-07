
# Account Group Role Create Request

Request to create a role assignment for an account group.

*This model accepts additional fields of type object.*

## Structure

`AccountGroupRoleCreateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `UserId` | `Guid` | Required | Unique identifier of the user, as a UUID. |
| `EntityType` | `string` | Required | The entity type; must be `ACCOUNT_GROUP` for account group roles.<br><br>**Default**: `"ACCOUNT_GROUP"` |
| `EntityId` | `Guid` | Required | Account group unique identifier. |
| `RoleType` | `string` | Required, Constant | Role type to assign. This request supports only `GUARDIAN`.<br><br>* `GUARDIAN` — The user is a legal custodian of the child account group.<br><br>**Value**: `"GUARDIAN"` |
| `CustodyType` | [`CustodyType?`](../../doc/models/custody-type.md) | Optional | Custody type for child account groups.<br><br>* `SOLE_CUSTODY` — A single guardian has custody of the child account group.<br>* `JOINT_CUSTODY` — Multiple guardians are required for the child account group. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

AccountGroupRoleCreateRequest accountGroupRoleCreateRequest = new AccountGroupRoleCreateRequest
{
    UserId = new Guid("00001e16-0000-0000-0000-000000000000"),
    EntityType = "ACCOUNT_GROUP",
    EntityId = new Guid("00002064-0000-0000-0000-000000000000"),
    RoleType = "GUARDIAN",
    CustodyType = CustodyType.SoleCustody,
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

