
# Savings Plan Portfolio

Request body for creating a `PORTFOLIO`-type savings plan. Specifies the account, cash amount, currency, and schedule. The account's portfolio configuration determines the allocation.

*This model accepts additional fields of type object.*

## Structure

`SavingsPlanPortfolio`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `UserId` | `Guid` | Required | Unique identifier of the user, as a UUID. |
| `AccountId` | `Guid` | Required | Account unique identifier. |
| `Name` | `string` | Optional | Savings plan name |
| `Type` | `string` | Required | The type of savings plan must be "PORTFOLIO".<br><br>**Default**: `"PORTFOLIO"` |
| `CashAmount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `Currency` | [`Currency`](../../doc/models/currency.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |
| `StartDate` | `string` | Required | First date of the savings plan execution in YYYY-MM-DD format.<br><br>**Constraints**: *Pattern*: `^[12]\d{3}-(0[1-9]\|1[0-2])-(0[1-9]\|[12]\d\|3[01])$` |
| `Period` | [`Period1`](../../doc/models/period-1.md) | Required | Unit of time.<br><br>* WEEK -<br>* MONTH -<br>* YEAR - |
| `Interval` | `int` | Required | Number of periods between executions<br><br>**Default**: `1`<br><br>**Constraints**: `>= 1`, `<= 1000` |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

SavingsPlanPortfolio savingsPlanPortfolio = new SavingsPlanPortfolio
{
    UserId = new Guid("00001ada-0000-0000-0000-000000000000"),
    AccountId = new Guid("00000d6e-0000-0000-0000-000000000000"),
    Type = "PORTFOLIO",
    CashAmount = "cash_amount4",
    Currency = Currency.Eur,
    StartDate = "start_date0",
    Period = Period1.Month,
    Interval = 1,
    Name = "name6",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

