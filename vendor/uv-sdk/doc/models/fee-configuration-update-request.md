
# Fee Configuration Update Request

## Structure

`FeeConfigurationUpdateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `FeeModelId` | `Guid` | Required | Fee model unique identifier. |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

FeeConfigurationUpdateRequest feeConfigurationUpdateRequest = new FeeConfigurationUpdateRequest
{
    FeeModelId = new Guid("00001d6a-0000-0000-0000-000000000000"),
};
```

