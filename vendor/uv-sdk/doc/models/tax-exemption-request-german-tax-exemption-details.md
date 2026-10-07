
# Tax Exemption Request German Tax Exemption Details

## Structure

`TaxExemptionRequestGermanTaxExemptionDetails`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `TaxExemptionType` | [`TaxExemptionType?`](../../doc/models/tax-exemption-type.md) | Optional | Tax exemption type<br><br>* SINGLE - Tax exemption for Individual.<br>* MARRIED - Tax exemption for married couples. |
| `TaxExemptionAmount` | [`TaxExemptionCreateRequestTaxExemptionDetailsAmount`](../../doc/models/tax-exemption-create-request-tax-exemption-details-amount.md) | Required | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

TaxExemptionRequestGermanTaxExemptionDetails taxExemptionRequestGermanTaxExemptionDetails = new TaxExemptionRequestGermanTaxExemptionDetails
{
    TaxExemptionAmount = new TaxExemptionCreateRequestTaxExemptionDetailsAmount
    {
        Amount = "amount4",
        Currency = Currency.Eur,
    },
    TaxExemptionType = TaxExemptionType.Single,
};
```

