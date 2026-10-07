
# Account Transfers Response

## Structure

`AccountTransfersResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Account transfer request unique identifier. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `Direction` | [`Direction1`](../../doc/models/direction-1.md) | Required | Direction of the account transfer<br><br>* INCOMING - account transfer is incoming to the user.<br>* OUTGOING - account transfer is outgoing from the user. |
| `Status` | [`Status78`](../../doc/models/status-78.md) | Required | Status of the account transfer<br><br>* NEW - account transfer is created but not started processing.<br>* PROCESSING - account transfer is in processing.<br>* SETTLED - account transfer was successfully settled.<br>* PARTIALLY_SETTLED - account transfer was partially settled, while some instrument got cancelled.<br>* CANCELLED - account transfer was cancelled. |
| `TransferType` | `string` | Required, Constant | Type of the account transfer<br><br>* NO_OWNER_CHANGE - No change of ownership.<br><br>**Value**: `"NO_OWNER_CHANGE"` |
| `Instruments` | [`List<Instrument10>`](../../doc/models/instrument-10.md) | Required | List of instruments positions. |
| `UserId` | `Guid` | Required | Unique identifier of the user, as a UUID. |
| `AccountId` | `Guid` | Required | Account unique identifier. |
| `SettlementReference` | `string` | Required | Unique identifier of the account transfer set by API consumers. Useful for API consumers to build special logic on top of it.<br>*NOTE: This reference will be set and be common to all securities transfers.*<br><br>**Constraints**: *Pattern*: `^[0-9A-Za-z+?/\-:()\.,' ]*$` |
| `Counterparty` | [`AccountTransferCounterpartyBic`](../../doc/models/account-transfer-counterparty-bic.md) | Required | Counterparty for account transfer. The `type` field determines which counterparty variant is present in the payload. |
| `TradeDate` | `DateTime?` | Optional | The forecast date when the trade takes place. Date in YYYY-MM-DD format.<br>*NOTE: This date mirrors the preliminary date if given in request as input, each single transfer can have their own date according to market needs.* |
| `SettlementDate` | `DateTime?` | Optional | The forecast date when the settlement takes place. Date in YYYY-MM-DD format.<br>*NOTE: This date mirrors the preliminary date if given in request as input, each single transfer can have their own date according to market needs.* |
| `OrderDate` | `DateTime?` | Optional | Date the transfer request was received from the end user. The regulatory deadline runs from this date. Date in YYYY-MM-DD format. |
| `DelayReason` | [`DelayReasonCode?`](../../doc/models/delay-reason-code.md) | Optional | Categorised reason why a securities transfer is delayed past the regulatory deadline.<br><br>* `COUNTERPARTY_NOT_INSTRUCTING` - The counterparty has not yet instructed the transfer.<br>* `COUNTERPARTY_NOT_RESPONDING` - The counterparty is not responding.<br>* `COUNTERPARTY_DOES_NOT_AGREE_TO_DELIVERY_DETAILS` - The counterparty does not agree to the delivery details.<br>* `COUNTERPARTY_NOT_ACCEPTING` - The counterparty is not accepting the transfer. |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

AccountTransfersResponse accountTransfersResponse = new AccountTransfersResponse
{
    Id = new Guid("00000736-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    Direction = Direction1.Incoming,
    Status = Status78.PartiallySettled,
    TransferType = "NO_OWNER_CHANGE",
    Instruments = new List<Instrument10>
    {
        new Instrument10
        {
            Id = "id8",
            IdType = "ISIN",
            Quantity = "quantity4",
            Status = Status79.Settled,
            TransferId = new Guid("0000199a-0000-0000-0000-000000000000"),
            QuantitySettled = "quantity_settled8",
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
    },
    UserId = new Guid("00000e46-0000-0000-0000-000000000000"),
    AccountId = new Guid("000000da-0000-0000-0000-000000000000"),
    SettlementReference = "settlement_reference2",
    Counterparty = new AccountTransferCounterpartyBic
    {
        Type = "BIC",
        Id = "id8",
        AccountNumber = "account_number2",
        Name = "name8",
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    TradeDate = DateTime.Parse("2016-03-13"),
    SettlementDate = DateTime.Parse("2016-03-13"),
    OrderDate = DateTime.Parse("2016-03-13"),
    DelayReason = DelayReasonCode.CounterpartyNotInstructing,
};
```

