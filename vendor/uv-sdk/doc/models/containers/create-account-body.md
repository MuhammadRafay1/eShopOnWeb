
# Create Account Body

## Class Name

`CreateAccountBody`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`AccountCreateUserRequest`](../../../doc/models/account-create-user-request.md) | CreateAccountBody.FromAccountCreateUserRequest(AccountCreateUserRequest accountCreateUserRequest) |
| [`AccountCreateBusinessRequest`](../../../doc/models/account-create-business-request.md) | CreateAccountBody.FromAccountCreateBusinessRequest(AccountCreateBusinessRequest accountCreateBusinessRequest) |

## AccountCreateUserRequest

### Initialization Code

#### Example

```csharp
CreateAccountBody value = CreateAccountBody.FromAccountCreateUserRequest(
    new AccountCreateUserRequest
    {
        UserId = new Guid("00001768-0000-0000-0000-000000000000"),
        AccountGroupId = new Guid("000022d2-0000-0000-0000-000000000000"),
        Type = Type16.Trading,
    }
);
```

## AccountCreateBusinessRequest

### Initialization Code

#### Example

```csharp
CreateAccountBody value = CreateAccountBody.FromAccountCreateBusinessRequest(
    new AccountCreateBusinessRequest
    {
        BusinessId = new Guid("000022be-0000-0000-0000-000000000000"),
        AccountGroupId = new Guid("0000257a-0000-0000-0000-000000000000"),
        Type = Type16.Trading,
    }
);
```

