
# Account Group

## Structure

`AccountGroup`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Account group unique identifier. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `Users` | [`List<User>`](../../doc/models/user.md) | Required | - |
| `Status` | [`Status18`](../../doc/models/status-18.md) | Required | Status of the account group<br><br>* PENDING_APPROVAL - Account group approval is pending - the account group is visible through our API but cannot be acted on.<br>* ACTIVE - Account group is active - full functionality of the Investment API is accessible.<br>* CLOSING - Account group is closing.<br>* CLOSED - Account group is closed.<br>* LOCKED - Account group is locked for all actions. |
| `Type` | [`Type13`](../../doc/models/type-13.md) | Required | Account group type.<br><br>* PERSONAL - Account group of a person holding assets on their own behalf.<br>* LEGAL_ENTITY - Account group of a legal entity holding assets on behalf of their users.<br>* FRENCH_PEA - Account group of a french resident holding assets in Plan d'Epargne en Actions.<br>* ISA - Account group of a UK resident holding assets in an individual savings account.<br>* CHILD - Account group of a child user holding assets in a child account. |
| `SecuritiesAccountNumber` | `string` | Required | Account unique identifier. |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

AccountGroup accountGroup = new AccountGroup
{
    Id = new Guid("00001a36-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    Users = new List<User>
    {
        new User
        {
            Id = new Guid("0000150a-0000-0000-0000-000000000000"),
            Type = Type12.Child,
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
    },
    Status = Status18.Closed,
    Type = Type13.Personal,
    SecuritiesAccountNumber = "securities_account_number0",
};
```

