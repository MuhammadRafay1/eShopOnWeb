
# Account Groups List Response 1

## Structure

`AccountGroupsListResponse1`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<AccountGroupsListResponse1Data>`](../../doc/models/containers/account-groups-list-response-1-data.md) | Required | This is List of a container for one-of cases. |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Models.Containers;
using UpvestInvestmentApi.Standard.Utilities;

AccountGroupsListResponse1 accountGroupsListResponse1 = new AccountGroupsListResponse1
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
    Data = new List<AccountGroupsListResponse1Data>
    {
        AccountGroupsListResponse1Data.FromAccountGroup(
            new AccountGroup
            {
                Id = new Guid("0000256c-0000-0000-0000-000000000000"),
                CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                    provider: CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                    provider: CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                Users = new List<User>
                {
                    new User
                    {
                        Id = new Guid("0000150a-0000-0000-0000-000000000000"),
                        Type = Type12.Child,
                        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
                    },
                    new User
                    {
                        Id = new Guid("0000150a-0000-0000-0000-000000000000"),
                        Type = Type12.Child,
                        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
                    },
                },
                Status = Status18.Closed,
                Type = Type13.Personal,
                SecuritiesAccountNumber = "securities_account_number0",
            }
        ),
    },
};
```

