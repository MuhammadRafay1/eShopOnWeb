
# Settlement Counterparties

Settlement counterparties for the securities transfer.
When `settlement_counterparties` is provided, `settlement_agent` is required. Other participants are optional but must respect the following dependency rules:

* `settlement_custodian` presence requires `settlement_party` to be present.
* `settlement_intermediary_1` presence requires `settlement_custodian` to be present.
* `settlement_intermediary_2` presence requires `settlement_intermediary_1` to be present.

Note: `settlement_custodian` can be provided without an explicit `settlement_party` since the default `counterparty` field at transfer level serves as the settlement party.

## Structure

`SettlementCounterparties`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `SettlementAgent` | [`SettlementCounterparty`](../../doc/models/settlement-counterparty.md) | Required | A participant in the settlement chain, identified by an identification and an optional account. |
| `SettlementCustodian` | [`SettlementCounterparty`](../../doc/models/settlement-counterparty.md) | Optional | A participant in the settlement chain, identified by an identification and an optional account. |
| `SettlementIntermediary1` | [`SettlementCounterparty`](../../doc/models/settlement-counterparty.md) | Optional | A participant in the settlement chain, identified by an identification and an optional account. |
| `SettlementIntermediary2` | [`SettlementCounterparty`](../../doc/models/settlement-counterparty.md) | Optional | A participant in the settlement chain, identified by an identification and an optional account. |
| `SettlementParty` | [`SettlementCounterparty`](../../doc/models/settlement-counterparty.md) | Optional | A participant in the settlement chain, identified by an identification and an optional account. |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

SettlementCounterparties settlementCounterparties = new SettlementCounterparties
{
    SettlementAgent = new SettlementCounterparty
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
    },
    SettlementCustodian = new SettlementCounterparty
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
    },
    SettlementIntermediary1 = new SettlementCounterparty
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
    },
    SettlementIntermediary2 = new SettlementCounterparty
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
    },
    SettlementParty = new SettlementCounterparty
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
    },
};
```

