
# Tax Exemption German Tax Exemption Details

## Structure

`TaxExemptionGermanTaxExemptionDetails`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `TaxExemptionType` | [`TaxExemptionType`](../../doc/models/tax-exemption-type.md) | Required | Tax exemption type<br><br>* SINGLE - Tax exemption for Individual.<br>* MARRIED - Tax exemption for married couples. |
| `TaxExemptionAmount` | [`TaxExemptionCreateRequestTaxExemptionDetailsAmount`](../../doc/models/tax-exemption-create-request-tax-exemption-details-amount.md) | Required | - |
| `UtilizedAmount` | [`TaxExemptionCreateRequestTaxExemptionDetailsAmount`](../../doc/models/tax-exemption-create-request-tax-exemption-details-amount.md) | Required | - |
| `RemainingAmount` | [`TaxExemptionCreateRequestTaxExemptionDetailsAmount`](../../doc/models/tax-exemption-create-request-tax-exemption-details-amount.md) | Required | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

TaxExemptionGermanTaxExemptionDetails taxExemptionGermanTaxExemptionDetails = new TaxExemptionGermanTaxExemptionDetails
{
    TaxExemptionType = TaxExemptionType.Single,
    TaxExemptionAmount = new TaxExemptionCreateRequestTaxExemptionDetailsAmount
    {
        Amount = "amount4",
        Currency = Currency.Eur,
    },
    UtilizedAmount = new TaxExemptionCreateRequestTaxExemptionDetailsAmount
    {
        Amount = "amount0",
        Currency = Currency.Eur,
    },
    RemainingAmount = new TaxExemptionCreateRequestTaxExemptionDetailsAmount
    {
        Amount = "amount8",
        Currency = Currency.Eur,
    },
};
```

