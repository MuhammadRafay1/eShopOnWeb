
# Security Transaction List Response

## Structure

`SecurityTransactionListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<SecurityTransaction>`](../../doc/models/security-transaction.md) | Required | - |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

SecurityTransactionListResponse securityTransactionListResponse = new SecurityTransactionListResponse
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
    Data = new List<SecurityTransaction>
    {
        new SecurityTransaction
        {
            AccountGroupId = new Guid("00000794-0000-0000-0000-000000000000"),
            AccountId = new Guid("000015ce-0000-0000-0000-000000000000"),
            BookingDate = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            Delta = new SecurityTransactionDelta
            {
                Amount = "amount0",
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            },
            Id = new Guid("00001c2a-0000-0000-0000-000000000000"),
            Instrument = new Instrument8
            {
                Uuid = new Guid("00001fd8-0000-0000-0000-000000000000"),
                Isin = "isin4",
            },
            Type = TransactionType1.AttachmentCancellation,
            References = new List<SecurityTransactionReference>
            {
                new SecurityTransactionReference
                {
                    Id = new Guid("00000f98-0000-0000-0000-000000000000"),
                    Type = Type51.CorporateAction,
                },
            },
            UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            ValueDate = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
    },
};
```

