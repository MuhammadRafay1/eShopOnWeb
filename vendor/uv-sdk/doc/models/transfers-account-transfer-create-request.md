
# Transfers Account Transfer Create Request

## Structure

`TransfersAccountTransferCreateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Direction` | [`Direction`](../../doc/models/direction.md) | Required | Direction of the securities transfer<br><br>* `INCOMING` - Securities transfer is incoming to the user.<br>* OUTGOING - Securities transfer is outgoing from the user. |
| `UserId` | `Guid` | Required | Unique identifier of the user, as a UUID. |
| `AccountId` | `Guid` | Required | Account unique identifier. |
| `TransferType` | `string` | Required, Constant | Type of the securities transfer<br><br>* NO_OWNER_CHANGE - No change of ownership.<br><br>**Value**: `"NO_OWNER_CHANGE"` |
| `SettlementReference` | `string` | Required | Unique identifier of the account transfer set by API consumers. Useful for API consumers to build special logic on top of it.<br>*NOTE: This reference will be set and be common to all securities transfers*<br><br>**Constraints**: *Pattern*: `^[0-9A-Za-z+?/\-:()\.,' ]*$` |
| `Instruments` | [`List<InstrumentsRequest>`](../../doc/models/instruments-request.md) | Required | For `INCOMING` at least one instrument should be defined. For `OUTGOING` an empty list means full transfer of full units of the account. |
| `Counterparty` | [`AccountTransferCounterpartyBic`](../../doc/models/account-transfer-counterparty-bic.md) | Required | Counterparty for account transfer. The `type` field determines which counterparty variant is present in the payload. |
| `TradeDate` | `DateTime?` | Optional | Optional forecast date when the trade takes place. If provided, usually T+1 is sufficient. Depending on the market this means valid working days. Date in YYYY-MM-DD format.<br>*NOTE: Each individual transfer can differ based on market needs.* |
| `SettlementDate` | `DateTime?` | Optional | Optional forecast date when the settlement takes place. If provided, usually T+2 is sufficient or trade_date + 1. Depending on the market this means valid working days. Date in YYYY-MM-DD format.<br>*NOTE: Each individual transfer can differ based on market needs. |
| `OrderDate` | `DateTime?` | Optional | Optional date when the transfer request was received from the end user. If omitted, Upvest defaults `order_date` to the transfer's `created_at` timestamp. The regulatory deadline runs from this date. Date in YYYY-MM-DD format. |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

TransfersAccountTransferCreateRequest transfersAccountTransferCreateRequest = new TransfersAccountTransferCreateRequest
{
    Direction = Direction.Incoming,
    UserId = new Guid("00001bf0-0000-0000-0000-000000000000"),
    AccountId = new Guid("00000e84-0000-0000-0000-000000000000"),
    TransferType = "NO_OWNER_CHANGE",
    SettlementReference = "settlement_reference0",
    Instruments = new List<InstrumentsRequest>
    {
        new InstrumentsRequest
        {
            Id = "id8",
            IdType = "ISIN",
            Quantity = "quantity4",
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
    },
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
};
```

