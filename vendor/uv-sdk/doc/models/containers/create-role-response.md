
# Create Role Response

## Class Name

`CreateRoleResponse`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`AccountGroupRoleCreateResponse`](../../../doc/models/account-group-role-create-response.md) | CreateRoleResponse.FromAccountGroupRoleCreateResponse(AccountGroupRoleCreateResponse accountGroupRoleCreateResponse) |
| [`BusinessRole`](../../../doc/models/business-role.md) | CreateRoleResponse.FromBusinessRole(BusinessRole businessRole) |

## AccountGroupRoleCreateResponse

### Initialization Code

#### Example

```csharp
CreateRoleResponse value = CreateRoleResponse.FromAccountGroupRoleCreateResponse(
    new AccountGroupRoleCreateResponse
    {
        Id = new Guid("00000544-0000-0000-0000-000000000000"),
        CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        UserId = new Guid("00000c54-0000-0000-0000-000000000000"),
        EntityType = "ACCOUNT_GROUP",
        EntityId = new Guid("00000ea2-0000-0000-0000-000000000000"),
        RoleType = RoleType.Owner,
        Status = Status115.Deactivated,
    }
);
```

## BusinessRole

### Initialization Code

#### Example

```csharp
CreateRoleResponse value = CreateRoleResponse.FromBusinessRole(
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

