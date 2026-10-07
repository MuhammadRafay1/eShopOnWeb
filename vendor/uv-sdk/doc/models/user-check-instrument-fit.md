
# User Check Instrument Fit

Instrument fit check is completed by the client providing the user's answers to the instrument appropriateness or suitability questionnaire.

*This model accepts additional fields of type object.*

## Structure

`UserCheckInstrumentFit`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | User Check unique identifier. |
| `UserId` | `Guid` | Required | Unique identifier of the user, as a UUID. |
| `Type` | `string` | Required | The type of check must be INSTRUMENT_FIT.<br><br>**Default**: `"INSTRUMENT_FIT"` |
| `CheckConfirmedAt` | `DateTime` | Required | Completion date and time of the instrument fit check. |
| `Status` | [`Status8`](../../doc/models/status-8.md) | Required | Final status of the instrument fit check.<br><br>* IN_PROGRESS - Instrument fit check is in progress<br>* PASSED - Instrument fit check passed<br>* FAILED - Instrument fit check failed |
| `InstrumentSuitability` | [`InstrumentSuitability`](../../doc/models/instrument-suitability.md) | Required | Outcome of the user's instrument suitability assessment. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

UserCheckInstrumentFit userCheckInstrumentFit = new UserCheckInstrumentFit
{
    Id = new Guid("00000b9c-0000-0000-0000-000000000000"),
    UserId = new Guid("000012ac-0000-0000-0000-000000000000"),
    Type = "INSTRUMENT_FIT",
    CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    Status = Status8.Failed,
    InstrumentSuitability = new InstrumentSuitability
    {
        Suitability = false,
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

