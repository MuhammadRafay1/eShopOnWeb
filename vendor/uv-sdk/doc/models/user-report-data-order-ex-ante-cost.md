
# User Report Data Order Ex Ante Cost

Contents of the order-ex-ante cost report for a user.

## Structure

`UserReportDataOrderExAnteCost`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `HoldingPeriod` | [`HoldingPeriod`](../../doc/models/holding-period.md) | Optional | Holding period. |
| `Instrument` | [`Instrument2`](../../doc/models/instrument-2.md) | Optional | Instrument details. |
| `Venue` | [`Venue1`](../../doc/models/venue-1.md) | Optional | Venue details. |
| `Account` | [`Account8`](../../doc/models/account-8.md) | Optional | Account details. |
| `AccountGroup` | [`AccountGroup4`](../../doc/models/account-group-4.md) | Optional | Account group details. |
| `User` | [`User9`](../../doc/models/user-9.md) | Required | User details. |
| `Order` | [`Order54`](../../doc/models/order-54.md) | Optional | Order details. |
| `ReturnImpact` | [`ReturnImpact`](../../doc/models/return-impact.md) | Optional | Return impact. |
| `TotalCost` | [`OrderExAnteAggregatedCost`](../../doc/models/order-ex-ante-aggregated-cost.md) | Optional | Aggregated totals of product costs, service costs and third party payments. |
| `ProductCost` | [`OrderExAnteProductCost`](../../doc/models/order-ex-ante-product-cost.md) | Optional | All costs and associated charges related to the financial instrument. |
| `ServiceCost` | [`OrderExAnteServiceCost`](../../doc/models/order-ex-ante-service-cost.md) | Optional | All costs and associated charges related to the investment service(s) and/or ancillary services. |
| `ThirdPartyPayments` | [`ThirdPartyPayments`](../../doc/models/third-party-payments.md) | Optional | Third-party payments associated with the investment service. |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

UserReportDataOrderExAnteCost userReportDataOrderExAnteCost = new UserReportDataOrderExAnteCost
{
    User = new User9
    {
        FirstName = "first_name0",
        LastName = "last_name8",
        Salutation = Salutation.SalutationMale,
        Title = Title10.Magister,
        Address = new Address30
        {
            AddressLine1 = "address_line10",
            AddressLine2 = "address_line28",
            Postcode = "postcode0",
            Country = "country0",
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
};
```

