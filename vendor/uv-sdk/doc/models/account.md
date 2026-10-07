
# Account

## Structure

`Account`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Account unique identifier. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `AccountGroupId` | `Guid` | Required | Account group unique identifier. |
| `Type` | [`Type16`](../../doc/models/type-16.md) | Required | Account type.<br><br>* TRADING - Orders in accounts of this type are created on a specific instrument basis.<br>* PORTFOLIO - Orders in accounts of this type are created on a portfolio basis and additional portfolio functionality is available. |
| `Users` | [`List<User>`](../../doc/models/user.md) | Required | - |
| `AccountNumber` | `int` | Required | The serial account number of the account in the account group.<br><br>**Constraints**: `>= 1` |
| `Name` | `string` | Required | The name of the account.<br><br>**Constraints**: *Maximum Length*: `100` |
| `Status` | [`Status21`](../../doc/models/status-21.md) | Required | The status of the account<br><br>* PENDING_APPROVAL - Account approval is pending - the account is visible through our API but cannot be acted on.<br>* ACTIVE - Account is active - full functionality of the Investment API is accessible.<br>* CLOSING - Account is closing - only sell orders or the transfer of positions out are permissible before the account is closed.<br>* CLOSED - Account is closed with zero balance successfully.<br>* LOCKED - Account is locked for all actions. |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Account account = new Account
{
    Id = new Guid("000025e4-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    AccountGroupId = new Guid("0000114e-0000-0000-0000-000000000000"),
    Type = Type16.Trading,
    Users = new List<User>
    {
        new User
        {
            Id = new Guid("0000150a-0000-0000-0000-000000000000"),
            Type = Type12.Child,
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
    },
    AccountNumber = 144,
    Name = "name0",
    Status = Status21.Closing,
};
```

