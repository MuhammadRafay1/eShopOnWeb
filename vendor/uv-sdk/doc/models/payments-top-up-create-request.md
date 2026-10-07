
# Payments Top Up Create Request

Request body for creating a top-up. Specifies the account group to fund and the expected cash amount.

## Structure

`PaymentsTopUpCreateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `AccountGroupId` | `Guid` | Required | Account group unique identifier. |
| `CashAmount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | [`Currency`](../../doc/models/currency.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

PaymentsTopUpCreateRequest paymentsTopUpCreateRequest = new PaymentsTopUpCreateRequest
{
    AccountGroupId = new Guid("00000094-0000-0000-0000-000000000000"),
    CashAmount = "cash_amount0",
    Currency = Currency.Eur,
};
```

