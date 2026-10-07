
# Create Account Group Body

## Class Name

`CreateAccountGroupBody`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`AccountGroupCreateUserRequest`](../../../doc/models/account-group-create-user-request.md) | CreateAccountGroupBody.FromAccountGroupCreateUserRequest(AccountGroupCreateUserRequest accountGroupCreateUserRequest) |
| [`AccountGroupCreateBusinessRequest`](../../../doc/models/account-group-create-business-request.md) | CreateAccountGroupBody.FromAccountGroupCreateBusinessRequest(AccountGroupCreateBusinessRequest accountGroupCreateBusinessRequest) |

## AccountGroupCreateUserRequest

### Initialization Code

#### Example

```csharp
CreateAccountGroupBody value = CreateAccountGroupBody.FromAccountGroupCreateUserRequest(
    new AccountGroupCreateUserRequest
    {
        UserId = new Guid("00000f70-0000-0000-0000-000000000000"),
        Type = Type13.Child,
    }
);
```

## AccountGroupCreateBusinessRequest

### Initialization Code

#### Example

```csharp
CreateAccountGroupBody value = CreateAccountGroupBody.FromAccountGroupCreateBusinessRequest(
    new AccountGroupCreateBusinessRequest
    {
        BusinessId = new Guid("0000074e-0000-0000-0000-000000000000"),
        Type = "BUSINESS",
    }
);
```

