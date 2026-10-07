
# Create Account Group Response

## Class Name

`CreateAccountGroupResponse`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`AccountGroup`](../../../doc/models/account-group.md) | CreateAccountGroupResponse.FromAccountGroup(AccountGroup accountGroup) |
| [`BusinessAccountGroup`](../../../doc/models/business-account-group.md) | CreateAccountGroupResponse.FromBusinessAccountGroup(BusinessAccountGroup businessAccountGroup) |

## AccountGroup

### Initialization Code

#### Example

```csharp
CreateAccountGroupResponse value = CreateAccountGroupResponse.FromAccountGroup(
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
            },
        },
        Status = Status18.Closed,
        Type = Type13.Personal,
        SecuritiesAccountNumber = "securities_account_number0",
    }
);
```

## BusinessAccountGroup

### Initialization Code

#### Example

```csharp
CreateAccountGroupResponse value = CreateAccountGroupResponse.FromBusinessAccountGroup(
    new BusinessAccountGroup
    {
        Id = new Guid("00001b6e-0000-0000-0000-000000000000"),
        CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        BusinessId = new Guid("0000041c-0000-0000-0000-000000000000"),
        Status = Status18.Active,
        Type = "BUSINESS",
        SecuritiesAccountNumber = "securities_account_number2",
    }
);
```

