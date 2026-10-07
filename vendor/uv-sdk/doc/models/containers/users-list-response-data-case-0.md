
# Users List Response Data Case 0

## Class Name

`UsersListResponseDataCase0`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`List<UserByol>`](../../../doc/models/user-byol.md) | UsersListResponseDataCase0.FromListOfUserBYOL(List<UserByol> listOfUserByol) |
| [`List<UserTol>`](../../../doc/models/user-tol.md) | UsersListResponseDataCase0.FromListOfUserTOL(List<UserTol> listOfUserTol) |

## List<UserByol>

### Initialization Code

#### Example

```csharp
UsersListResponseDataCase0 value = UsersListResponseDataCase0.FromListOfUserBYOL(
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
);
```

## List<UserTol>

### Initialization Code

#### Example

```csharp
UsersListResponseDataCase0 value = UsersListResponseDataCase0.FromListOfUserTOL(
    new List<UserTol>
    {
        new UserTol
        {
            Id = new Guid("00000aa4-0000-0000-0000-000000000000"),
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
                Nationality.Md,
                Nationality.Mc,
            },
            Address = new Address
            {
                AddressLine1 = "address_line10",
                Postcode = "postcode0",
                Country = Country.Bf,
                City = "city6",
            },
            Status = Status.Active,
        },
    }
);
```

