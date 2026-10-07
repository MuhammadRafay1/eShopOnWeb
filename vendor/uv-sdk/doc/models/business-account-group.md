
# Business Account Group

*This model accepts additional fields of type object.*

## Structure

`BusinessAccountGroup`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Account group unique identifier. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `BusinessId` | `Guid` | Required | Unique identifier for the business. |
| `Status` | [`Status18`](../../doc/models/status-18.md) | Required | Status of the account group<br><br>* PENDING_APPROVAL - Account group approval is pending - the account group is visible through our API but cannot be acted on.<br>* ACTIVE - Account group is active - full functionality of the Investment API is accessible.<br>* CLOSING - Account group is closing.<br>* CLOSED - Account group is closed.<br>* LOCKED - Account group is locked for all actions. |
| `Type` | `string` | Required, Constant | Account group type.<br><br>* BUSINESS - Account group of a business holding assets.<br><br>**Value**: `"BUSINESS"` |
| `SecuritiesAccountNumber` | `string` | Required | Account unique identifier. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

BusinessAccountGroup businessAccountGroup = new BusinessAccountGroup
{
    Id = new Guid("000015aa-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    BusinessId = new Guid("00002568-0000-0000-0000-000000000000"),
    Status = Status18.Closed,
    Type = "BUSINESS",
    SecuritiesAccountNumber = "securities_account_number6",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

