
# Create User Body

## Class Name

`CreateUserBody`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`UserByolCreateRequest`](../../../doc/models/user-byol-create-request.md) | CreateUserBody.FromUserBYOLCreateRequest(UserByolCreateRequest userByolCreateRequest) |
| [`UserTolCreateRequest`](../../../doc/models/user-tol-create-request.md) | CreateUserBody.FromUserTOLCreateRequest(UserTolCreateRequest userTolCreateRequest) |

## UserByolCreateRequest

### Initialization Code

#### Example

```csharp
CreateUserBody value = CreateUserBody.FromUserBYOLCreateRequest(
    new UserByolCreateRequest
    {
        FirstName = "first_name0",
        LastName = "last_name8",
        BirthDate = DateTime.Parse("2016-03-13"),
        Nationalities = new List<Nationality>
        {
            Nationality.Il,
        },
        Address = new Address
        {
            AddressLine1 = "address_line10",
            Postcode = "postcode0",
            Country = Country.Bf,
            City = "city6",
        },
    }
);
```

## UserTolCreateRequest

### Initialization Code

#### Example

```csharp
CreateUserBody value = CreateUserBody.FromUserTOLCreateRequest(
    new UserTolCreateRequest
    {
        FirstName = "first_name8",
        LastName = "last_name6",
        Email = "email8",
        BirthDate = DateTime.Parse("2016-03-13"),
        Nationalities = new List<Nationality>
        {
            Nationality.Bj,
            Nationality.Bi,
        },
        Address = new Address
        {
            AddressLine1 = "address_line10",
            Postcode = "postcode0",
            Country = Country.Bf,
            City = "city6",
        },
        Fatca = new Fatca
        {
            Status = false,
            ConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
        },
    }
);
```

