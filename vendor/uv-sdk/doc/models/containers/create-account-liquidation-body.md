
# Create Account Liquidation Body

## Class Name

`CreateAccountLiquidationBody`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`AccountLiquidationRequest`](../../../doc/models/account-liquidation-request.md) | CreateAccountLiquidationBody.FromAccountLiquidationRequest(AccountLiquidationRequest accountLiquidationRequest) |
| [`AccountLiquidationRequest1`](../../../doc/models/account-liquidation-request-1.md) | CreateAccountLiquidationBody.FromAccountLiquidationRequest1(AccountLiquidationRequest1 accountLiquidationRequest1) |

## AccountLiquidationRequest

### Initialization Code

#### Example

```csharp
CreateAccountLiquidationBody value = CreateAccountLiquidationBody.FromAccountLiquidationRequest(
    new AccountLiquidationRequest
    {
        UserId = new Guid("0000262e-0000-0000-0000-000000000000"),
    }
);
```

## AccountLiquidationRequest1

### Initialization Code

#### Example

```csharp
CreateAccountLiquidationBody value = CreateAccountLiquidationBody.FromAccountLiquidationRequest1(
    new AccountLiquidationRequest1
    {
        BusinessId = new Guid("00001c72-0000-0000-0000-000000000000"),
    }
);
```

