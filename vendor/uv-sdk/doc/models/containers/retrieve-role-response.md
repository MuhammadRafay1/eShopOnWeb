
# Retrieve Role Response

## Class Name

`RetrieveRoleResponse`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`AccountGroupRole`](../../../doc/models/account-group-role.md) | RetrieveRoleResponse.FromAccountGroupRole(AccountGroupRole accountGroupRole) |
| [`BusinessRole`](../../../doc/models/business-role.md) | RetrieveRoleResponse.FromBusinessRole(BusinessRole businessRole) |

## AccountGroupRole

### Initialization Code

#### Example

```csharp
RetrieveRoleResponse value = RetrieveRoleResponse.FromAccountGroupRole(
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
        EntityType = "ACCOUNT_GROUP",
        EntityId = new Guid("00001688-0000-0000-0000-000000000000"),
        RoleType = RoleType.Child,
        Status = Status115.Active,
    }
);
```

## BusinessRole

### Initialization Code

#### Example

```csharp
RetrieveRoleResponse value = RetrieveRoleResponse.FromBusinessRole(
    new BusinessRole
    {
        Id = new Guid("00001814-0000-0000-0000-000000000000"),
        CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        UserId = new Guid("00001f24-0000-0000-0000-000000000000"),
        EntityType = "BUSINESS",
        EntityId = new Guid("00002172-0000-0000-0000-000000000000"),
        RoleType = RoleType1.Trader,
        Status = Status115.Pending,
    }
);
```

