
# Users List Response Data

## Class Name

`UsersListResponseData`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`UsersListResponseDataCase0`](../../../doc/models/containers/users-list-response-data-case-0.md) | UsersListResponseData.FromUsersListResponseDataCase0(UsersListResponseDataCase0 usersListResponseDataCase0) |
| `object` | UsersListResponseData.FromListOfObject(object listOfObject) |

## UsersListResponseDataCase0

### Initialization Code

#### Example

```csharp
UsersListResponseData value = UsersListResponseData.FromUsersListResponseDataCase0(
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
                },
                Status = Status.Offboarding,
            },
        }
    )
);
```

## object

### Initialization Code

#### Example

```csharp
UsersListResponseData value = UsersListResponseData.FromListOfObject(
    new List<object>
    {
        ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    }
);
```

