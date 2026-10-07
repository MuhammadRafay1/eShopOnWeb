
# Security Transfers List Response

## Structure

`SecurityTransfersListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<Datum9>`](../../doc/models/datum-9.md) | Required | - |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

SecurityTransfersListResponse securityTransfersListResponse = new SecurityTransfersListResponse
{
    Meta = new Meta
    {
        Offset = 222,
        Limit = 126,
        Count = 14,
        TotalCount = 150,
        Sort = "sort4",
        Order = Order1.Asc,
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    Data = new List<Datum9>
    {
        new Datum9
        {
            Id = new Guid("00001c2a-0000-0000-0000-000000000000"),
            CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            Direction = Direction4.Incoming,
            Status = Status79.Settled,
            TransferType = "NO_OWNER_CHANGE",
            InstrumentId = "instrument_id4",
            InstrumentIdType = "ISIN",
            Quantity = "quantity6",
            UserId = new Guid("0000233a-0000-0000-0000-000000000000"),
            AccountId = new Guid("000015ce-0000-0000-0000-000000000000"),
            SettlementReference = "settlement_reference6",
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
            QuantitySettled = "quantity_settled4",
            PlaceOfSettlement = "place_of_settlement8",
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
        },
    },
};
```

