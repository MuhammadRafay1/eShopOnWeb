
# Tax Residencies Set Request

Request payload for setting a user's tax residencies. The submitted set replaces any previously recorded tax residencies.

## Structure

`TaxResidenciesSetRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `TaxResidencies` | [`List<TaxResidenciesSetRequestTaxResidencies>`](../../doc/models/containers/tax-residencies-set-request-tax-residencies.md) | Required | This is List of a container for one-of cases.<br><br>**Constraints**: *Minimum Items*: `1` |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Models.Containers;
using UpvestInvestmentApi.Standard.Utilities;

TaxResidenciesSetRequest taxResidenciesSetRequest = new TaxResidenciesSetRequest
{
    TaxResidencies = new List<TaxResidenciesSetRequestTaxResidencies>
    {
        TaxResidenciesSetRequestTaxResidencies.FromWithTaxIdentifierNumber(
            new WithTaxIdentifierNumber
            {
                Country = Country.Mr,
                TaxIdentifierNumber = "tax_identifier_number6",
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            }
        ),
        TaxResidenciesSetRequestTaxResidencies.FromWithTaxIdentifierNumber(
            new WithTaxIdentifierNumber
            {
                Country = Country.Mr,
                TaxIdentifierNumber = "tax_identifier_number6",
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            }
        ),
    },
};
```

