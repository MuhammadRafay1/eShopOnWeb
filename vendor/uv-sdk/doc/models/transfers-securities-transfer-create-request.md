
# Transfers Securities Transfer Create Request

## Structure

`TransfersSecuritiesTransferCreateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Direction` | [`Direction4`](../../doc/models/direction-4.md) | Required | Direction of the securities transfer<br><br>* `INCOMING` - Securities transfer is incoming to the user.<br>* `OUTGOING` - Securities transfer is outgoing from the user. |
| `InstrumentId` | `string` | Required | `ISIN` or other identity (depends on instrument_id_type) of the security to be transferred. |
| `InstrumentIdType` | `string` | Required, Constant | Type of the instrument_id<br><br>* `ISIN` - International Securities Identification Number<br><br>**Value**: `"ISIN"` |
| `Quantity` | `string` | Required | The quantity of instrument to move in or out. The value supported is maximum 15 digits including decimal place.<br>*Note: For `INCOMING` the end user ensures that they don't sell their instruments on the counter-broker to enable smooth transfer of their instruments on Upvest platform.*<br><br>**Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `TransferType` | `string` | Required, Constant | Type of the securities transfer<br><br>* NO_OWNER_CHANGE - No change of ownership.<br><br>**Value**: `"NO_OWNER_CHANGE"` |
| `UserId` | `Guid` | Required | Unique identifier of the user, as a UUID. |
| `AccountId` | `Guid` | Required | Account unique identifier. |
| `PlaceOfSettlement` | `string` | Optional | Business Identifier Code (also known as SWIFT-BIC, BIC, SWIFT ID or SWIFT code) [ISO 9362](https://en.wikipedia.org/wiki/ISO_9362).<br><br>**Constraints**: *Pattern*: `^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$` |
| `SettlementReference` | `string` | Required | Unique identifier of the securities transfer set by API consumers. Useful for API consumers to build special logic on top of it.<br>*NOTE: For automatic incoming transfers where API users will subscribe to the corresponding webhook, the value is set by Upvest!*<br>*TIP: For non-live environment if you prefix the reference with `AUTO` your transfer gets through all lifecycle states.*<br><br>**Constraints**: *Pattern*: `^[0-9A-Za-z+?/\-:()\.,' ]*$` |
| `Counterparty` | [`SecuritiesTransferCounterpartyBic`](../../doc/models/securities-transfer-counterparty-bic.md) | Required | Counterparty for securities transfer. The `type` field determines which counterparty variant is present in the payload. |
| `SettlementCounterparties` | [`SettlementCounterparties`](../../doc/models/settlement-counterparties.md) | Optional | Settlement counterparties for the securities transfer.<br>When `settlement_counterparties` is provided, `settlement_agent` is required. Other participants are optional but must respect the following dependency rules:<br><br>* `settlement_custodian` presence requires `settlement_party` to be present.<br>* `settlement_intermediary_1` presence requires `settlement_custodian` to be present.<br>* `settlement_intermediary_2` presence requires `settlement_intermediary_1` to be present.<br><br>Note: `settlement_custodian` can be provided without an explicit `settlement_party` since the default `counterparty` field at transfer level serves as the settlement party. |
| `TradeDate` | `DateTime?` | Optional | Optional forecast date when the trade takes place. If provided, usually T+1 is sufficient. Depending on the market this means valid working days. Date in YYYY-MM-DD format. |
| `SettlementDate` | `DateTime?` | Optional | Optional forecast date when the settlement takes place. If provided, usually T+2 is sufficient or trade_date + 1. Depending on the market this means valid working days. Date in YYYY-MM-DD format. |
| `OrderDate` | `DateTime?` | Optional | Optional date when the transfer request was received from the end user. If omitted, Upvest defaults `order_date` to the transfer's `created_at` timestamp. The regulatory deadline runs from this date. Date in YYYY-MM-DD format. |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

TransfersSecuritiesTransferCreateRequest transfersSecuritiesTransferCreateRequest = new TransfersSecuritiesTransferCreateRequest
{
    Direction = Direction4.Incoming,
    InstrumentId = "instrument_id0",
    InstrumentIdType = "ISIN",
    Quantity = "quantity2",
    TransferType = "NO_OWNER_CHANGE",
    UserId = new Guid("000009a0-0000-0000-0000-000000000000"),
    AccountId = new Guid("00002344-0000-0000-0000-000000000000"),
    SettlementReference = "settlement_reference2",
    Counterparty = new SecuritiesTransferCounterpartyBic
    {
        Type = "BIC",
        Id = "id8",
        AccountNumber = "account_number2",
        Name = "name8",
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    PlaceOfSettlement = "place_of_settlement6",
    SettlementCounterparties = new SettlementCounterparties
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
    },
    TradeDate = DateTime.Parse("2016-03-13"),
    SettlementDate = DateTime.Parse("2016-03-13"),
    OrderDate = DateTime.Parse("2016-03-13"),
};
```

