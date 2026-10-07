
# Allocation

## Structure

`Allocation`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `InstrumentId` | [`AllocationInstrumentId`](../../doc/models/containers/allocation-instrument-id.md) | Required | This is a container for one-of cases. |
| `InstrumentIdType` | [`InstrumentIdType4`](../../doc/models/instrument-id-type-4.md) | Required | The type of the ID used in the request.<br><br>* ISIN - International Securities Identification Number<br>* UPVEST - UPVEST's unique instrument identifier<br><br>**Default**: `InstrumentIdType4.ISIN` |
| `Weight` | `string` | Required | Instrument allocation weight<br><br>**Constraints**: *Pattern*: `^(100\|[0-9]{1,2}(\.[0-9]{1,5})?)$` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Models.Containers;

Allocation allocation = new Allocation
{
    InstrumentId = AllocationInstrumentId.FromString("String3"),
    InstrumentIdType = InstrumentIdType4.Isin,
    Weight = "weight6",
};
```

