
# Tax Exemptions Update Response

## Structure

`TaxExemptionsUpdateResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid?` | Optional | Tax Exemption Unique Identifier |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

TaxExemptionsUpdateResponse taxExemptionsUpdateResponse = new TaxExemptionsUpdateResponse
{
    Id = new Guid("00001e6a-0000-0000-0000-000000000000"),
};
```

