
# Users List Response

Paginated response containing the list of all users.

## Structure

`UsersListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`UsersListResponseData`](../../doc/models/containers/users-list-response-data.md) | Required | This is a container for one-of cases. |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Models.Containers;
using UpvestInvestmentApi.Standard.Utilities;

UsersListResponse usersListResponse = new UsersListResponse
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
    Data = UsersListResponseData.FromUsersListResponseDataCase0(
        UsersListResponseDataCase0.FromListOfUserBYOL(
            new List<UserByol>
            {
                new UserByol
                {
                    Id = new Guid("0000239a-0000-0000-0000-000000000000"),
                    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                        provider: CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind),
                    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                        provider: CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind),
                    FirstName = "first_name4",
                    LastName = "last_name2",
                    BirthDate = DateTime.Parse("2016-03-13"),
                    Nationalities = new List<Nationality>
                    {
                        Nationality.Nc,
                        Nationality.Na,
                    },
                    Address = new Address
                    {
                        AddressLine1 = "address_line10",
                        Postcode = "postcode0",
                        Country = Country.Bf,
                        City = "city6",
                        AddressLine2 = "address_line28",
                        State = "state2",
                        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
                    },
                    Status = Status.Offboarding,
                    Salutation = Salutation.SalutationFemaleMarried,
                    Title = Title.Dr,
                    BirthCity = "birth_city4",
                    BirthCountry = BirthCountry.St,
                    BirthName = "birth_name4",
                },
            }
        )
    ),
};
```

