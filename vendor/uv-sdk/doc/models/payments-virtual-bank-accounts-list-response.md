
# Payments Virtual Bank Accounts List Response

Paginated list of virtual bank accounts for an account group, including cursor-based pagination metadata.

## Structure

`PaymentsVirtualBankAccountsListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<Datum6>`](../../doc/models/datum-6.md) | Required | - |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

PaymentsVirtualBankAccountsListResponse paymentsVirtualBankAccountsListResponse = new PaymentsVirtualBankAccountsListResponse
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
    Data = new List<Datum6>
    {
        new Datum6
        {
            Id = new Guid("00001c2a-0000-0000-0000-000000000000"),
            CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            AccountGroupId = new Guid("00000794-0000-0000-0000-000000000000"),
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
            Name = "name0",
        },
    },
};
```

