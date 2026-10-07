
# Withdrawals List Response

Paginated list of cash withdrawals for an account group, including cursor-based pagination metadata.

## Structure

`WithdrawalsListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<PaymentsWithdrawal>`](../../doc/models/payments-withdrawal.md) | Required | - |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

WithdrawalsListResponse withdrawalsListResponse = new WithdrawalsListResponse
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
    Data = new List<PaymentsWithdrawal>
    {
        new PaymentsWithdrawal
        {
            Id = new Guid("00001c2a-0000-0000-0000-000000000000"),
            CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            ReferenceAccountId = new Guid("00001f66-0000-0000-0000-000000000000"),
            AccountGroupId = new Guid("00000794-0000-0000-0000-000000000000"),
            Amount = "amount2",
            Currency = Currency.Eur,
            RemittanceInformation = "remittance_information6",
            UserId = new Guid("0000233a-0000-0000-0000-000000000000"),
            Taxes = new List<Tax>
            {
                new Tax
                {
                    Amount = "amount2",
                    Currency = Currency.Eur,
                    Type = "type0",
                    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
                },
            },
            Status = Status34.Confirmed,
            CancellationReason = CancellationReasonCodeForWithdrawal.CancelledByClient,
            PurposeCode = "purpose_code6",
        },
    },
};
```

