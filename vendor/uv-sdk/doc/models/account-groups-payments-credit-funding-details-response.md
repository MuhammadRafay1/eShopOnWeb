
# Account Groups Payments Credit Funding Details Response

Virtual bank account details for funding an account group via SEPA Credit Transfer. Each entry includes an IBAN, BIC, and `remittance_information` the end user must include in their bank transfer.

## Structure

`AccountGroupsPaymentsCreditFundingDetailsResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<Datum4>`](../../doc/models/datum-4.md) | Required | - |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

AccountGroupsPaymentsCreditFundingDetailsResponse accountGroupsPaymentsCreditFundingDetailsResponse = new AccountGroupsPaymentsCreditFundingDetailsResponse
{
    Meta = new Meta
    {
        Offset = 222,
        Limit = 126,
        Count = 14,
        TotalCount = 150,
        Sort = "sort4",
        Order = Order1.Asc,
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    Data = new List<Datum4>
    {
        new Datum4
        {
            Currency = Currency.Eur,
            Owner = new Owner
            {
                Name = "name4",
            },
            Identification = new Identification
            {
                Swift = new Swift
                {
                    Iban = "iban2",
                    Bic = "bic0",
                },
            },
            VirtualBankAccountId = new Guid("000005a0-0000-0000-0000-000000000000"),
            Name = "name0",
            RemittanceInformation = "remittance_information6",
        },
    },
};
```

