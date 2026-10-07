
# Portfolios Order Place Request

Request body for placing a portfolio order. Specifies the account, cash amount, currency, and order side (`BUY` or `SELL`).

## Structure

`PortfoliosOrderPlaceRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `UserId` | `Guid` | Required | Unique identifier of the user, as a UUID. |
| `AccountId` | `Guid` | Required | Account unique identifier. |
| `CashAmount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `Currency` | [`Currency`](../../doc/models/currency.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |
| `Side` | [`Side15`](../../doc/models/side-15.md) | Required | Side of the portfolio order.<br><br>* BUY -<br>* SELL - |
| `PostTax` | `bool?` | Optional | Cash amount is post-tax value<br><br>**Default**: `false` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

PortfoliosOrderPlaceRequest portfoliosOrderPlaceRequest = new PortfoliosOrderPlaceRequest
{
    UserId = new Guid("000014ce-0000-0000-0000-000000000000"),
    AccountId = new Guid("00000762-0000-0000-0000-000000000000"),
    CashAmount = "cash_amount6",
    Currency = Currency.Eur,
    Side = Side15.Buy,
    PostTax = false,
};
```

