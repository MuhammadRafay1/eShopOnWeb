
# User Check Compliance

A compliance check conducted by Upvest for a user, including the check and user identifiers.

*This model accepts additional fields of type object.*

## Structure

`UserCheckCompliance`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | User Check unique identifier. |
| `UserId` | `Guid` | Required | Unique identifier of the user, as a UUID. |
| `Type` | `string` | Required | The type of check must be COMPLIANCE.<br><br>**Default**: `"COMPLIANCE"` |
| `Status` | [`Status9?`](../../doc/models/status-9.md) | Optional | Final status of the COMPLIANCE check.<br><br>* IN_PROGRESS - Compliance check is in progress<br>* PASSED - Compliance check passed<br>* FAILED - Compliance check failed |
| `CheckConfirmedAt` | `DateTime` | Required | Completion date and time of the COMPLIANCE check. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

UserCheckCompliance userCheckCompliance = new UserCheckCompliance
{
    Id = new Guid("000018a6-0000-0000-0000-000000000000"),
    UserId = new Guid("00001fb6-0000-0000-0000-000000000000"),
    Type = "COMPLIANCE",
    CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    Status = Status9.Passed,
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

