
# Settlement Counterparty

A participant in the settlement chain, identified by an identification and an optional account.

## Structure

`SettlementCounterparty`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Identification` | [`SettlementCounterpartyIdentification`](../../doc/models/settlement-counterparty-identification.md) | Required | Identification of a settlement counterparty.<br><br>* `BIC` Party identified by BIC.<br>* `PROPRIETARY` Party identified by a proprietary code under a Data Source Scheme.<br>* `NAME_AND_ADDRESS` Party identified by name and address. |
| `Account` | [`SettlementCounterpartyAccount`](../../doc/models/settlement-counterparty-account.md) | Optional | Account of a settlement counterparty.<br><br>* `SAFE` - Safekeeping account. |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

SettlementCounterparty settlementCounterparty = new SettlementCounterparty
{
    Identification = new SettlementCounterpartyIdentification
    {
        Type = Type52.Bic,
        MValue = "value4",
        Scheme = "scheme2",
    },
    Account = new SettlementCounterpartyAccount
    {
        Type = "type0",
        MValue = "value2",
    },
};
```

