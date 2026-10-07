
# Tax Wrappers Create Isa Tax Wrapper Request

## Structure

`TaxWrappersCreateIsaTaxWrapperRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `AccountGroupId` | `Guid` | Required | The ID of the account group to which the tax wrapper belongs. |
| `Type` | `string` | Required, Constant | Types of the ISA tax wrapper<br><br>**Value**: `"STOCKS_AND_SHARES_ISA"` |
| `IsFlexible` | `bool` | Required | True if ISA is flexible. |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

TaxWrappersCreateIsaTaxWrapperRequest taxWrappersCreateIsaTaxWrapperRequest = new TaxWrappersCreateIsaTaxWrapperRequest
{
    AccountGroupId = new Guid("0000167a-0000-0000-0000-000000000000"),
    Type = "STOCKS_AND_SHARES_ISA",
    IsFlexible = false,
};
```

