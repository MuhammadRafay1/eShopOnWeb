
# Create Account Response

## Class Name

`CreateAccountResponse`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`Account`](../../../doc/models/account.md) | CreateAccountResponse.FromAccount(Account account) |
| [`BusinessAccount`](../../../doc/models/business-account.md) | CreateAccountResponse.FromBusinessAccount(BusinessAccount businessAccount) |

## Account

### Initialization Code

#### Example

```csharp
CreateAccountResponse value = CreateAccountResponse.FromAccount(
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
            },
        },
        AccountNumber = 188,
        Name = "name8",
        Status = Status21.PendingApproval,
    }
);
```

## BusinessAccount

### Initialization Code

#### Example

```csharp
CreateAccountResponse value = CreateAccountResponse.FromBusinessAccount(
    new BusinessAccount
    {
        Id = new Guid("00000408-0000-0000-0000-000000000000"),
        CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        AccountGroupId = new Guid("00001682-0000-0000-0000-000000000000"),
        Type = Type16.Trading,
        BusinessId = new Guid("000013c6-0000-0000-0000-000000000000"),
        AccountNumber = 124,
        Name = "name2",
        Status = Status21.Active,
    }
);
```

