
# Create User Response

## Class Name

`CreateUserResponse`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`UserByol`](../../../doc/models/user-byol.md) | CreateUserResponse.FromUserBYOL(UserByol userByol) |
| [`UserTol`](../../../doc/models/user-tol.md) | CreateUserResponse.FromUserTOL(UserTol userTol) |

## UserByol

### Initialization Code

#### Example

```csharp
CreateUserResponse value = CreateUserResponse.FromUserBYOL(
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
    }
);
```

## UserTol

### Initialization Code

#### Example

```csharp
CreateUserResponse value = CreateUserResponse.FromUserTOL(
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
    }
);
```

