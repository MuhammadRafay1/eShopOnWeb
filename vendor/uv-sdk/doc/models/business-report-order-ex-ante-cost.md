
# Business Report Order Ex Ante Cost

*This model accepts additional fields of type object.*

## Structure

`BusinessReportOrderExAnteCost`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Report unique identifier. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `BusinessId` | `Guid` | Required | Unique identifier for the business. |
| `Type` | `string` | Required, Constant | The type of report must be “ORDER_EX_ANTE_COST”. Savings plan ex-ante reports are not supported for business entities.<br><br>**Value**: `"ORDER_EX_ANTE_COST"` |
| `SubstitutedReportId` | `Guid?` | Required | - |
| `Data` | [`BusinessReportDataOrderExAnteCost`](../../doc/models/business-report-data-order-ex-ante-cost.md) | Optional | Contents of the order ex-ante cost report for a business. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

BusinessReportOrderExAnteCost businessReportOrderExAnteCost = new BusinessReportOrderExAnteCost
{
    Id = new Guid("00000d7c-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    BusinessId = new Guid("00001d3a-0000-0000-0000-000000000000"),
    Type = "ORDER_EX_ANTE_COST",
    SubstitutedReportId = new Guid("00001d24-0000-0000-0000-000000000000"),
    Data = new BusinessReportDataOrderExAnteCost
    {
        Business = new Business
        {
            CompanyName = "company_name8",
            Address = new Address
            {
                AddressLine1 = "address_line10",
                Postcode = "postcode0",
                Country = Country.Bf,
                City = "city6",
                AddressLine2 = "address_line28",
                State = "state2",
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            },
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
        HoldingPeriod = new HoldingPeriod
        {
            Unit = Unit.Year,
            Quantity = 138,
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
        Instrument = new Instrument2
        {
            Isin = "isin4",
            ShortName = "short_name2",
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
        Venue = new Venue1
        {
            Name = "name8",
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
        Account = new Account8
        {
            Id = new Guid("000025e4-0000-0000-0000-000000000000"),
            AccountNumber = 144,
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
        AccountGroup = new AccountGroup4
        {
            Id = new Guid("000026d6-0000-0000-0000-000000000000"),
            SecuritiesAccountNumber = "securities_account_number2",
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
    },
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

