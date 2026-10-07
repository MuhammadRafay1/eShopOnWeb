
# Instruments Request

*This model accepts additional fields of type object.*

## Structure

`InstrumentsRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `string` | Required | `ISIN` or other identity (depends on instrument_id_type) of the security to be transferred. |
| `IdType` | `string` | Required, Constant | Type of the instrument_id<br><br>* `ISIN` - International Securities Identification Number<br><br>**Value**: `"ISIN"` |
| `Quantity` | `string` | Required | The quantity of instrument to move in or out. The value supported is maximum 15 digits including decimal place.<br>*Note: For `INCOMING` the end user ensures that they don't sell their instruments on the counter-broker to enable smooth transfer of their instruments on Upvest platform.*<br><br>**Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

InstrumentsRequest instrumentsRequest = new InstrumentsRequest
{
    Id = "id0",
    IdType = "ISIN",
    Quantity = "quantity6",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

