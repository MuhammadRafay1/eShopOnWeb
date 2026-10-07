
# Accounts List Response 1

## Structure

`AccountsListResponse1`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<AccountsListResponse1Data>`](../../doc/models/containers/accounts-list-response-1-data.md) | Required | This is List of a container for one-of cases. |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Models.Containers;
using UpvestInvestmentApi.Standard.Utilities;

AccountsListResponse1 accountsListResponse1 = new AccountsListResponse1
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
    Data = new List<AccountsListResponse1Data>
    {
        AccountsListResponse1Data.FromAccount(
            new Account
            {
                Id = new Guid("00000120-0000-0000-0000-000000000000"),
                CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                    provider: CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                    provider: CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                AccountGroupId = new Guid("0000139a-0000-0000-0000-000000000000"),
                Type = Type16.Trading,
                Users = new List<User>
                {
                    new User
                    {
                        Id = new Guid("0000150a-0000-0000-0000-000000000000"),
                        Type = Type12.Child,
                        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
                    },
                },
                AccountNumber = 188,
                Name = "name8",
                Status = Status21.PendingApproval,
            }
        ),
        AccountsListResponse1Data.FromAccount(
            new Account
            {
                Id = new Guid("00000120-0000-0000-0000-000000000000"),
                CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                    provider: CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                    provider: CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                AccountGroupId = new Guid("0000139a-0000-0000-0000-000000000000"),
                Type = Type16.Trading,
                Users = new List<User>
                {
                    new User
                    {
                        Id = new Guid("0000150a-0000-0000-0000-000000000000"),
                        Type = Type12.Child,
                        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
                    },
                },
                AccountNumber = 188,
                Name = "name8",
                Status = Status21.PendingApproval,
            }
        ),
        AccountsListResponse1Data.FromAccount(
            new Account
            {
                Id = new Guid("00000120-0000-0000-0000-000000000000"),
                CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                    provider: CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                    provider: CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                AccountGroupId = new Guid("0000139a-0000-0000-0000-000000000000"),
                Type = Type16.Trading,
                Users = new List<User>
                {
                    new User
                    {
                        Id = new Guid("0000150a-0000-0000-0000-000000000000"),
                        Type = Type12.Child,
                        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
                    },
                },
                AccountNumber = 188,
                Name = "name8",
                Status = Status21.PendingApproval,
            }
        ),
    },
};
```

