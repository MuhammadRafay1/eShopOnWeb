
# Isa Transfers Request

## Structure

`IsaTransfersRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Direction` | [`Direction7`](../../doc/models/direction-7.md) | Required | Direction of the ISA transfer<br><br>* INCOMING - Transfer is incoming to the user.<br>* OUTGOING - Transfer is outgoing from the user. |
| `TransferType` | [`TransferType`](../../doc/models/transfer-type.md) | Required | Type of the securities transfer<br><br>* ISA_INTERNAL - Transfer occurs within the same ISA manager.<br>* ISA_EXTERNAL - Transfer occurs across different ISA managers, via Equisoft or others. |
| `TransferValue` | `string` | Optional | **Constraints**: *Pattern*: `^[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | `string` | Required, Constant | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* GBP - Great British Pound<br><br>**Value**: `"GBP"` |
| `TransferMethod` | `string` | Required, Constant | Method of the ISA transfer<br><br>* CASH - Cash transfer.<br><br>Other methods can be supported in the future, e.g. IN_SPECIE<br><br>**Value**: `"CASH"` |
| `UserId` | `Guid` | Required | Unique identifier of the user, as a UUID. |
| `AccountGroupId` | `Guid` | Required | Account group unique identifier. |
| `Reference` | `string` | Optional | Random string reference on which API clients can build logic.<br><br>**Constraints**: *Pattern*: `^[0-9A-Za-z+?/\-:()\.,' ]*$` |
| `Counterparty` | [`Counterparty2`](../../doc/models/counterparty-2.md) | Optional | - |
| `TransferDate` | `DateTime` | Required | The date when end user initiated the transfer. Relevant for tax year's end reporting. If not sent it will be the time we create the transfer internally on our side. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `Details` | [`Details2`](../../doc/models/details-2.md) | Optional | - |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;

IsaTransfersRequest isaTransfersRequest = new IsaTransfersRequest
{
    Direction = Direction7.Incoming,
    TransferType = TransferType.IsaInternal,
    Currency = "GBP",
    TransferMethod = "CASH",
    UserId = new Guid("0000227e-0000-0000-0000-000000000000"),
    AccountGroupId = new Guid("000006d8-0000-0000-0000-000000000000"),
    TransferDate = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    TransferValue = "transfer_value2",
    Reference = "reference2",
    Counterparty = new Counterparty2
    {
        AccountNumber = "account_number2",
    },
    Details = new Details2
    {
        CurrentYearSubscription = new CurrentYearSubscription
        {
            TransferAmount = "transfer_amount6",
            FirstSubscriptionAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
        },
    },
};
```

