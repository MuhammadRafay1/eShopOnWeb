
# Tax Exemptions Create Response

## Structure

`TaxExemptionsCreateResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid?` | Optional | Tax Exemption Unique Identifier |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

TaxExemptionsCreateResponse taxExemptionsCreateResponse = new TaxExemptionsCreateResponse
{
    Id = new Guid("00002538-0000-0000-0000-000000000000"),
};
```

