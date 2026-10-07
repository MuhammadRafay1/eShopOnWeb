
# Mandates List Response

Paginated list of direct debit mandates for a user, including cursor-based pagination metadata.

## Structure

`MandatesListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<DirectDebitMandate>`](../../doc/models/direct-debit-mandate.md) | Required | - |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

MandatesListResponse mandatesListResponse = new MandatesListResponse
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
    Data = new List<DirectDebitMandate>
    {
        new DirectDebitMandate
        {
            Id = new Guid("00001c2a-0000-0000-0000-000000000000"),
            CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            Iban = "iban4",
            Bic = "bic2",
            CreditorName = "creditor_name4",
            CreditorId = "creditor_id2",
            CreditorAddress = new Address
            {
                AddressLine1 = "address_line12",
                Postcode = "postcode2",
                Country = Country.Mt,
                City = "city8",
                AddressLine2 = "address_line20",
                State = "state4",
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            },
            Type = "RECURRENT",
            ConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            UserId = new Guid("0000233a-0000-0000-0000-000000000000"),
            BusinessId = new Guid("000004d8-0000-0000-0000-000000000000"),
        },
    },
};
```

