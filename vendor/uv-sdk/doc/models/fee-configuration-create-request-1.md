
# Fee Configuration Create Request 1

## Structure

`FeeConfigurationCreateRequest1`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `AccountId` | `Guid` | Required | Account unique identifier. |
| `FeeModelId` | `Guid` | Required | Fee model unique identifier. |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

FeeConfigurationCreateRequest1 feeConfigurationCreateRequest1 = new FeeConfigurationCreateRequest1
{
    AccountId = new Guid("00001d34-0000-0000-0000-000000000000"),
    FeeModelId = new Guid("00001abc-0000-0000-0000-000000000000"),
};
```

