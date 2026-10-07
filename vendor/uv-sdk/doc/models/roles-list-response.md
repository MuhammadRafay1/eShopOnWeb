
# Roles List Response

Paginated list of roles. Contains a `data` array of role objects and a `meta` object with offset/limit pagination metadata.

## Structure

`RolesListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<RolesListResponseData>`](../../doc/models/containers/roles-list-response-data.md) | Required | This is List of a container for one-of cases. |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Models.Containers;
using UpvestInvestmentApi.Standard.Utilities;

RolesListResponse rolesListResponse = new RolesListResponse
{
    Meta = new Meta
    {
        Offset = 222,
        Limit = 126,
        Count = 14,
        TotalCount = 150,
        Sort = "sort4",
        Order = Order1.Asc,
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    Data = new List<RolesListResponseData>
    {
        RolesListResponseData.FromAccountGroupRole(
            new AccountGroupRole
            {
                Id = new Guid("00000d2a-0000-0000-0000-000000000000"),
                CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                    provider: CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                    provider: CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                UserId = new Guid("0000143a-0000-0000-0000-000000000000"),
                EntityType = "entity_type8",
                EntityId = new Guid("00001688-0000-0000-0000-000000000000"),
                RoleType = RoleType.Child,
                Status = Status115.Active,
                CustodyType = CustodyType.SoleCustody,
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            }
        ),
        RolesListResponseData.FromAccountGroupRole(
            new AccountGroupRole
            {
                Id = new Guid("00000d2a-0000-0000-0000-000000000000"),
                CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                    provider: CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                    provider: CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                UserId = new Guid("0000143a-0000-0000-0000-000000000000"),
                EntityType = "entity_type8",
                EntityId = new Guid("00001688-0000-0000-0000-000000000000"),
                RoleType = RoleType.Child,
                Status = Status115.Active,
                CustodyType = CustodyType.SoleCustody,
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            }
        ),
    },
};
```

