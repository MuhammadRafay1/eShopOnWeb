
# Instrument 10

*This model accepts additional fields of type object.*

## Structure

`Instrument10`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `string` | Required | `ISIN` or other identity (depends on instrument_id_type) of the security to be transferred. |
| `IdType` | `string` | Required, Constant | Type of the instrument_id<br><br>* `ISIN` - International Securities Identification Number<br><br>**Value**: `"ISIN"` |
| `Quantity` | `string` | Required | The quantity of instrument to move in or out.<br><br>**Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `QuantitySettled` | `string` | Optional | The quantity of instruments settled.<br><br>**Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `Status` | [`Status79`](../../doc/models/status-79.md) | Required | Status of the securities transfer<br><br>* `NEW` - Securities transfer is created but not started processing.<br>* `PROCESSING` - Securities transfer is in processing.<br>* `SETTLED` - Securities transfer was successfully settled.<br>* `CANCELLED` - Securities transfer was cancelled. |
| `TransferId` | `Guid` | Required | Securities transfer request unique identifier. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Instrument10 instrument10 = new Instrument10
{
    Id = "id8",
    IdType = "ISIN",
    Quantity = "quantity4",
    Status = Status79.Settled,
    TransferId = new Guid("0000252a-0000-0000-0000-000000000000"),
    QuantitySettled = "quantity_settled8",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

