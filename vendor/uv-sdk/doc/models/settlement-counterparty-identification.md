
# Settlement Counterparty Identification

Identification of a settlement counterparty.

* `BIC` Party identified by BIC.
* `PROPRIETARY` Party identified by a proprietary code under a Data Source Scheme.
* `NAME_AND_ADDRESS` Party identified by name and address.

## Structure

`SettlementCounterpartyIdentification`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Type` | [`Type52`](../../doc/models/type-52.md) | Required | Type of identification.<br><br>* `BIC` - Party identified by BIC.<br>* `PROPRIETARY` - Party identified by a proprietary code under a Data Source Scheme.<br>* `NAME_AND_ADDRESS` - Party identified by name and address. |
| `MValue` | `string` | Required | Identification value. The format depends on the `type`:<br><br>* For `BIC`: A valid BIC code.<br>* For `PROPRIETARY`: A proprietary identifier under the given `scheme`.<br>* For `NAME_AND_ADDRESS`: Name and address of the party. |
| `Scheme` | `string` | Optional | ISO Data Source Scheme code. **Required** when `type` is `PROPRIETARY`. Identifies the source of the proprietary identification (e.g. `CRST`, `DAKV`, `CEDE`).<br><br>**Constraints**: *Pattern*: `^[A-Z]{1,8}$` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

SettlementCounterpartyIdentification settlementCounterpartyIdentification = new SettlementCounterpartyIdentification
{
    Type = Type52.Proprietary,
    MValue = "value8",
    Scheme = "scheme6",
};
```

