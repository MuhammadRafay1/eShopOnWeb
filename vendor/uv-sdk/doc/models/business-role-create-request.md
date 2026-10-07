
# Business Role Create Request

Request to create a role for a business entity.

*This model accepts additional fields of type object.*

## Structure

`BusinessRoleCreateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `UserId` | `Guid` | Required | Unique identifier of the user, as a UUID. |
| `EntityType` | `string` | Required | The entity type; must be `BUSINESS` for business roles.<br><br>**Default**: `"BUSINESS"` |
| `EntityId` | `Guid` | Required | Unique identifier for the business. |
| `RoleType` | [`RoleType1`](../../doc/models/role-type-1.md) | Required | Role type for a business entity.<br><br>* `LEGAL_REPRESENTATIVE` — The user is a legal representative of the business.<br>* `AUTHORISED_SIGNATORY` — The user is authorised to sign documents and make commitments on behalf of the business.<br>* `ULTIMATE_BENEFICIAL_OWNER` — The user ultimately owns or controls the business.<br>* `CONTRACTING_EXECUTIVE` — The user is able to enter into contracts on behalf of the business.<br>* `TRADER` — The user is authorised to place orders on behalf of the business.<br>* `SOLE_TRADER` — The user places orders on behalf of a sole trader entity. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

BusinessRoleCreateRequest businessRoleCreateRequest = new BusinessRoleCreateRequest
{
    UserId = new Guid("0000256c-0000-0000-0000-000000000000"),
    EntityType = "BUSINESS",
    EntityId = new Guid("000000aa-0000-0000-0000-000000000000"),
    RoleType = RoleType1.UltimateBeneficialOwner,
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

