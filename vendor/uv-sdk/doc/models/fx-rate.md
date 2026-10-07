
# Fx Rate

Entity representing the applied FX rate for a currency conversion.

## Structure

`FxRate`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `AllInRate` | `string` | Required | The all-in rate applied to the conversion, including any markup.<br><br>**Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `BaseRate` | `string` | Required | The base rate applied to the conversion, excluding markup.<br><br>**Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `MarkupRate` | `string` | Required | The markup applied on top of the base rate.<br><br>**Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

FxRate fxRate = new FxRate
{
    AllInRate = "all_in_rate6",
    BaseRate = "base_rate4",
    MarkupRate = "markup_rate4",
};
```

