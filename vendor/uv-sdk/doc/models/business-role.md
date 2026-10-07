
# Business Role

Role assignment for a business entity.

*This model accepts additional fields of type object.*

## Structure

`BusinessRole`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Unique identifier for the role. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UserId` | `Guid` | Required | Unique identifier of the user, as a UUID. |
| `EntityType` | `string` | Required | The entity type; must be `BUSINESS` for business roles.<br><br>**Default**: `"BUSINESS"` |
| `EntityId` | `Guid` | Required | Unique identifier of the entity a role is attached to. |
| `RoleType` | [`RoleType1`](../../doc/models/role-type-1.md) | Required | Role type for a business entity.<br><br>* `LEGAL_REPRESENTATIVE` — The user is a legal representative of the business.<br>* `AUTHORISED_SIGNATORY` — The user is authorised to sign documents and make commitments on behalf of the business.<br>* `ULTIMATE_BENEFICIAL_OWNER` — The user ultimately owns or controls the business.<br>* `CONTRACTING_EXECUTIVE` — The user is able to enter into contracts on behalf of the business.<br>* `TRADER` — The user is authorised to place orders on behalf of the business.<br>* `SOLE_TRADER` — The user places orders on behalf of a sole trader entity. |
| `Status` | [`Status115`](../../doc/models/status-115.md) | Required | Status of the role assignment.<br><br>* `PENDING` — The role has been created but is not yet active.<br>* `ACTIVE` — The role is active.<br>* `DEACTIVATED` — The role has been deactivated and cannot be reactivated. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

BusinessRole businessRole = new BusinessRole
{
    Id = new Guid("00001e56-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UserId = new Guid("00002566-0000-0000-0000-000000000000"),
    EntityType = "BUSINESS",
    EntityId = new Guid("000000a4-0000-0000-0000-000000000000"),
    RoleType = RoleType1.Trader,
    Status = Status115.Pending,
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

