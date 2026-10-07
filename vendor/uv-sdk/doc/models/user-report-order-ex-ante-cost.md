
# User Report Order Ex Ante Cost

*This model accepts additional fields of type object.*

## Structure

`UserReportOrderExAnteCost`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Report unique identifier. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UserId` | `Guid` | Required | Unique identifier of the user, as a UUID. |
| `Type` | [`Type44`](../../doc/models/type-44.md) | Required | The type of report must be “ORDER_EX_ANTE_COST” or “ORDER_EX_ANTE_COST_SAVINGS_PLAN”. |
| `SubstitutedReportId` | `Guid?` | Required | - |
| `Data` | [`UserReportDataOrderExAnteCost`](../../doc/models/user-report-data-order-ex-ante-cost.md) | Optional | Contents of the order-ex-ante cost report for a user. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

UserReportOrderExAnteCost userReportOrderExAnteCost = new UserReportOrderExAnteCost
{
    Id = new Guid("00000f28-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UserId = new Guid("00001638-0000-0000-0000-000000000000"),
    Type = Type44.OrderExAnteCost,
    SubstitutedReportId = new Guid("00001ed0-0000-0000-0000-000000000000"),
    Data = new UserReportDataOrderExAnteCost
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
    },
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

