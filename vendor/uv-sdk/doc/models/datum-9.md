
# Datum 9

## Structure

`Datum9`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Securities transfer request unique identifier. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `Direction` | [`Direction4`](../../doc/models/direction-4.md) | Required | Direction of the securities transfer<br><br>* `INCOMING` - Securities transfer is incoming to the user.<br>* `OUTGOING` - Securities transfer is outgoing from the user. |
| `Status` | [`Status79`](../../doc/models/status-79.md) | Required | Status of the securities transfer<br><br>* `NEW` - Securities transfer is created but not started processing.<br>* `PROCESSING` - Securities transfer is in processing.<br>* `SETTLED` - Securities transfer was successfully settled.<br>* `CANCELLED` - Securities transfer was cancelled. |
| `TransferType` | `string` | Required, Constant | Type of the securities transfer<br><br>* `NO_OWNER_CHANGE` - No change of ownership.<br><br>**Value**: `"NO_OWNER_CHANGE"` |
| `InstrumentId` | `string` | Required | `ISIN` or other identity (depends on instrument_id_type) of the security to be transferred. |
| `InstrumentIdType` | `string` | Required, Constant | Type of the instrument_id<br><br>* `ISIN` - International Securities Identification Number<br><br>**Value**: `"ISIN"` |
| `Quantity` | `string` | Required | The quantity of instrument to move in or out.<br><br>**Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `QuantitySettled` | `string` | Optional | The quantity of instruments settled.<br><br>**Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `UserId` | `Guid` | Required | Unique identifier of the user, as a UUID. |
| `AccountId` | `Guid` | Required | Account unique identifier. |
| `PlaceOfSettlement` | `string` | Optional | Business Identifier Code (also known as SWIFT-BIC, BIC, SWIFT ID or SWIFT code) [ISO 9362](https://en.wikipedia.org/wiki/ISO_9362).<br><br>**Constraints**: *Pattern*: `^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$` |
| `SettlementReference` | `string` | Required | Unique identifier of the securities transfer set by API consumers. Useful for API consumers to build special logic on top of it.<br>*NOTE: For automatic incoming transfers where API users will subscribe to the corresponding webhook, the value is set by Upvest!*<br><br>**Constraints**: *Pattern*: `^[0-9A-Za-z+?/\-:()\.,' ]*$` |
| `Counterparty` | [`SecuritiesTransferCounterpartyBic`](../../doc/models/securities-transfer-counterparty-bic.md) | Required | Counterparty for securities transfer. The `type` field determines which counterparty variant is present in the payload. |
| `SettlementCounterparties` | [`SettlementCounterparties`](../../doc/models/settlement-counterparties.md) | Optional | Settlement counterparties for the securities transfer.<br>When `settlement_counterparties` is provided, `settlement_agent` is required. Other participants are optional but must respect the following dependency rules:<br><br>* `settlement_custodian` presence requires `settlement_party` to be present.<br>* `settlement_intermediary_1` presence requires `settlement_custodian` to be present.<br>* `settlement_intermediary_2` presence requires `settlement_intermediary_1` to be present.<br><br>Note: `settlement_custodian` can be provided without an explicit `settlement_party` since the default `counterparty` field at transfer level serves as the settlement party. |
| `TradeDate` | `DateTime` | Required | The forecast date when the trade takes place. Date in YYYY-MM-DD format. |
| `SettlementDate` | `DateTime` | Required | The forecast date when the settlement takes place. Date in YYYY-MM-DD format. |
| `ActualSettlementDate` | `DateTime?` | Optional | The date when the transfer settled, only known in `SETTLED` status. Date in YYYY-MM-DD format. |
| `OrderDate` | `DateTime?` | Optional | Optional. Date the transfer request was received from the end user. The regulatory deadline runs from this date. Date in YYYY-MM-DD format. |
| `DelayReason` | [`DelayReasonCode?`](../../doc/models/delay-reason-code.md) | Optional | Categorised reason why a securities transfer is delayed past the regulatory deadline.<br><br>* `COUNTERPARTY_NOT_INSTRUCTING` - The counterparty has not yet instructed the transfer.<br>* `COUNTERPARTY_NOT_RESPONDING` - The counterparty is not responding.<br>* `COUNTERPARTY_DOES_NOT_AGREE_TO_DELIVERY_DETAILS` - The counterparty does not agree to the delivery details.<br>* `COUNTERPARTY_NOT_ACCEPTING` - The counterparty is not accepting the transfer. |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Datum9 datum9 = new Datum9
{
    Id = new Guid("00001980-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    Direction = Direction4.Incoming,
    Status = Status79.New,
    TransferType = "NO_OWNER_CHANGE",
    InstrumentId = "instrument_id2",
    InstrumentIdType = "ISIN",
    Quantity = "quantity4",
    UserId = new Guid("00002090-0000-0000-0000-000000000000"),
    AccountId = new Guid("00001324-0000-0000-0000-000000000000"),
    SettlementReference = "settlement_reference4",
    Counterparty = new SecuritiesTransferCounterpartyBic
    {
        Type = "BIC",
        Id = "id8",
        AccountNumber = "account_number2",
        Name = "name8",
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    TradeDate = DateTime.Parse("2016-03-13"),
    SettlementDate = DateTime.Parse("2016-03-13"),
    QuantitySettled = "quantity_settled8",
    PlaceOfSettlement = "place_of_settlement4",
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
    ActualSettlementDate = DateTime.Parse("2016-03-13"),
    OrderDate = DateTime.Parse("2016-03-13"),
};
```

