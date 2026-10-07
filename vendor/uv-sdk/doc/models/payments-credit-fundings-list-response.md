
# Payments Credit Fundings List Response

Paginated list of credit fundings for an account group, including cursor-based pagination metadata.

## Structure

`PaymentsCreditFundingsListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<Datum5>`](../../doc/models/datum-5.md) | Required | - |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

PaymentsCreditFundingsListResponse paymentsCreditFundingsListResponse = new PaymentsCreditFundingsListResponse
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
    Data = new List<Datum5>
    {
        new Datum5
        {
            Id = new Guid("00001c2a-0000-0000-0000-000000000000"),
            CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            AccountGroupId = new Guid("00000794-0000-0000-0000-000000000000"),
            CashAmount = "cash_amount8",
            Currency = Currency.Eur,
            RemittanceInformation = "remittance_information6",
            Counterparty = new Counterparty
            {
                Identification = new Identification1
                {
                    Name = "name2",
                    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
                },
                Account = new Account3
                {
                    Identification = new Identification2
                    {
                        Iban = "iban6",
                        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
                    },
                    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
                },
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            },
            Status = Status32.Confirmed,
            VirtualBankAccountId = new Guid("000005a0-0000-0000-0000-000000000000"),
            PurposeCode = "purpose_code6",
        },
    },
};
```

