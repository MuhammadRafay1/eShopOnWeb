
# User Check Instrument Fit Create Request

Instrument fit check is completed by the client providing the user's answers to the instrument appropriateness or suitability questionnaire.

*This model accepts additional fields of type object.*

## Structure

`UserCheckInstrumentFitCreateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Type` | `string` | Required | The type of check must be INSTRUMENT_FIT.<br><br>**Default**: `"INSTRUMENT_FIT"` |
| `CheckConfirmedAt` | `DateTime` | Required | Completion date and time of the instrument fit check. |
| `InstrumentSuitability` | [`InstrumentSuitability`](../../doc/models/instrument-suitability.md) | Required | Outcome of the user's instrument suitability assessment. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

UserCheckInstrumentFitCreateRequest userCheckInstrumentFitCreateRequest = new UserCheckInstrumentFitCreateRequest
{
    Type = "INSTRUMENT_FIT",
    CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    InstrumentSuitability = new InstrumentSuitability
    {
        Suitability = false,
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

