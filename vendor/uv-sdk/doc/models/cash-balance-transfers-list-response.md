
# Cash Balance Transfers List Response

Paginated list of cash balance transfers, including cursor-based pagination metadata.

## Structure

`CashBalanceTransfersListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<PaymentsCashBalanceTransfer>`](../../doc/models/payments-cash-balance-transfer.md) | Required | - |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

CashBalanceTransfersListResponse cashBalanceTransfersListResponse = new CashBalanceTransfersListResponse
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
    Data = new List<PaymentsCashBalanceTransfer>
    {
        new PaymentsCashBalanceTransfer
        {
            Id = new Guid("00001c2a-0000-0000-0000-000000000000"),
            CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            SourceAccountGroupId = new Guid("00000122-0000-0000-0000-000000000000"),
            TargetAccountGroupId = new Guid("00001d8e-0000-0000-0000-000000000000"),
            Amount = "amount2",
            Currency = Currency.Eur,
            Status = Status37.Cancelled,
            CancellationReason = "cancellation_reason8",
        },
    },
};
```

