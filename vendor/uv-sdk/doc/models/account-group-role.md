
# Account Group Role

Role assignment for an account group.

*This model accepts additional fields of type object.*

## Structure

`AccountGroupRole`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Unique identifier for the role. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UserId` | `Guid` | Required | Unique identifier of the user, as a UUID. |
| `EntityType` | `string` | Required | The entity type; must be `ACCOUNT_GROUP` for account group roles.<br><br>**Default**: `"ACCOUNT_GROUP"` |
| `EntityId` | `Guid` | Required | Unique identifier of the entity a role is attached to. |
| `RoleType` | [`RoleType`](../../doc/models/role-type.md) | Required | Role type for an account group.<br><br>* `OWNER` — The user owns the account group.<br>* `GUARDIAN` — The user is a legal custodian of a child account group.<br>* `CHILD` — The user is the child beneficiary of a child account group. |
| `CustodyType` | [`CustodyType?`](../../doc/models/custody-type.md) | Optional | Custody type for child account groups.<br><br>* `SOLE_CUSTODY` — A single guardian has custody of the child account group.<br>* `JOINT_CUSTODY` — Multiple guardians are required for the child account group. |
| `Status` | [`Status115`](../../doc/models/status-115.md) | Required | Status of the role assignment.<br><br>* `PENDING` — The role has been created but is not yet active.<br>* `ACTIVE` — The role is active.<br>* `DEACTIVATED` — The role has been deactivated and cannot be reactivated. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

AccountGroupRole accountGroupRole = new AccountGroupRole
{
    Id = new Guid("00001066-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UserId = new Guid("00001776-0000-0000-0000-000000000000"),
    EntityType = "ACCOUNT_GROUP",
    EntityId = new Guid("000019c4-0000-0000-0000-000000000000"),
    RoleType = RoleType.Guardian,
    Status = Status115.Pending,
    CustodyType = CustodyType.SoleCustody,
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

