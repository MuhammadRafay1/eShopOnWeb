
# Create Role Body

## Class Name

`CreateRoleBody`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`AccountGroupRoleCreateRequest`](../../../doc/models/account-group-role-create-request.md) | CreateRoleBody.FromAccountGroupRoleCreateRequest(AccountGroupRoleCreateRequest accountGroupRoleCreateRequest) |
| [`BusinessRoleCreateRequest`](../../../doc/models/business-role-create-request.md) | CreateRoleBody.FromBusinessRoleCreateRequest(BusinessRoleCreateRequest businessRoleCreateRequest) |

## AccountGroupRoleCreateRequest

### Initialization Code

#### Example

```csharp
CreateRoleBody value = CreateRoleBody.FromAccountGroupRoleCreateRequest(
    new AccountGroupRoleCreateRequest
    {
        UserId = new Guid("000020d0-0000-0000-0000-000000000000"),
        EntityType = "ACCOUNT_GROUP",
        EntityId = new Guid("000003f2-0000-0000-0000-000000000000"),
        RoleType = "GUARDIAN",
    }
);
```

## BusinessRoleCreateRequest

### Initialization Code

#### Example

```csharp
CreateRoleBody value = CreateRoleBody.FromBusinessRoleCreateRequest(
    new BusinessRoleCreateRequest
    {
        UserId = new Guid("00001d56-0000-0000-0000-000000000000"),
        EntityType = "BUSINESS",
        EntityId = new Guid("00001fa4-0000-0000-0000-000000000000"),
        RoleType = RoleType1.Trader,
    }
);
```

