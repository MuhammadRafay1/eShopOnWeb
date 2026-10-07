
# Savings Plan Instrument 2

Request body for creating an `INSTRUMENT`-type savings plan. Specifies the account, instrument, cash amount, currency, schedule, and optional fee configuration.

*This model accepts additional fields of type object.*

## Structure

`SavingsPlanInstrument2`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `UserId` | `Guid` | Required | Unique identifier of the user, as a UUID. |
| `AccountId` | `Guid` | Required | Account unique identifier. |
| `Name` | `string` | Optional | Savings plan name |
| `Type` | `string` | Required | The type of savings plan must be "INSTRUMENT".<br><br>**Default**: `"INSTRUMENT"` |
| `InstrumentId` | [`SavingsPlanInstrument2InstrumentId`](../../doc/models/containers/savings-plan-instrument-2-instrument-id.md) | Optional | This is a container for one-of cases. |
| `InstrumentIdType` | [`InstrumentIdType10?`](../../doc/models/instrument-id-type-10.md) | Optional | The type of the ID used in the request.<br><br>* ISIN - International Securities Identification Number<br>* WKN - German securities identification code<br><br>**Default**: `InstrumentIdType10.ISIN` |
| `CashAmount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `Currency` | [`Currency`](../../doc/models/currency.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |
| `StartDate` | `string` | Required | First date of the savings plan execution in YYYY-MM-DD format.<br><br>**Constraints**: *Pattern*: `^[12]\d{3}-(0[1-9]\|1[0-2])-(0[1-9]\|[12]\d\|3[01])$` |
| `Period` | [`Period1`](../../doc/models/period-1.md) | Required | Unit of time.<br><br>* WEEK -<br>* MONTH -<br>* YEAR - |
| `Interval` | `int` | Required | Number of periods between executions<br><br>**Default**: `1`<br><br>**Constraints**: `>= 1`, `<= 1000` |
| `FeeConfiguration` | [`List<SavingsPlanFeeConfigurationOnlyForInstrument>`](../../doc/models/savings-plan-fee-configuration-only-for-instrument.md) | Optional | Fee configuration for instrument-type savings plan executions. Specifies the transaction fee model to apply to each buy order placed on execution. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Models.Containers;
using UpvestInvestmentApi.Standard.Utilities;

SavingsPlanInstrument2 savingsPlanInstrument2 = new SavingsPlanInstrument2
{
    UserId = new Guid("00001574-0000-0000-0000-000000000000"),
    AccountId = new Guid("00000808-0000-0000-0000-000000000000"),
    Type = "INSTRUMENT",
    CashAmount = "cash_amount2",
    Currency = Currency.Eur,
    StartDate = "start_date8",
    Period = Period1.Month,
    Interval = 1,
    Name = "name4",
    InstrumentId = SavingsPlanInstrument2InstrumentId.FromString("String9"),
    InstrumentIdType = InstrumentIdType10.Isin,
    FeeConfiguration = new List<SavingsPlanFeeConfigurationOnlyForInstrument>
    {
        new SavingsPlanFeeConfigurationOnlyForInstrument
        {
            Type = "type2",
            TransactionFeeModelId = new Guid("0000158e-0000-0000-0000-000000000000"),
        },
    },
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

