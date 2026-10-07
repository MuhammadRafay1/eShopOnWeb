
# Create Business Response

## Class Name

`CreateBusinessResponse`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`BusinessCompanyCreateResponse`](../../../doc/models/business-company-create-response.md) | CreateBusinessResponse.FromBusinessCompanyCreateResponse(BusinessCompanyCreateResponse businessCompanyCreateResponse) |
| [`BusinessSoleTraderCreateResponse`](../../../doc/models/business-sole-trader-create-response.md) | CreateBusinessResponse.FromBusinessSoleTraderCreateResponse(BusinessSoleTraderCreateResponse businessSoleTraderCreateResponse) |

## BusinessCompanyCreateResponse

### Initialization Code

#### Example

```csharp
CreateBusinessResponse value = CreateBusinessResponse.FromBusinessCompanyCreateResponse(
    new BusinessCompanyCreateResponse
    {
    }
);
```

## BusinessSoleTraderCreateResponse

### Initialization Code

#### Example

```csharp
CreateBusinessResponse value = CreateBusinessResponse.FromBusinessSoleTraderCreateResponse(
    new BusinessSoleTraderCreateResponse
    {
        BusinessType = "SOLE_TRADER",
    }
);
```

