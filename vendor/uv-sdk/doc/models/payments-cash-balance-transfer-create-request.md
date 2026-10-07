
# Payments Cash Balance Transfer Create Request

Request body for creating a cash balance transfer between two account groups belonging to the same user and tenant.

## Structure

`PaymentsCashBalanceTransferCreateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `SourceAccountGroupId` | `Guid` | Required | The account group the cash is transferred from. Must differ from `target_account_group_id` and belong to the same user and tenant. Allowed account group types are `PERSONAL`, `CHILD`, and `BUSINESS`, and the source and target account groups must be of the same type. |
| `TargetAccountGroupId` | `Guid` | Required | The account group the cash is transferred to. Must differ from `source_account_group_id` and belong to the same user and tenant. Allowed account group types are `PERSONAL`, `CHILD`, and `BUSINESS`, and the source and target account groups must be of the same type. |
| `Amount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | [`Currency`](../../doc/models/currency.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

PaymentsCashBalanceTransferCreateRequest paymentsCashBalanceTransferCreateRequest = new PaymentsCashBalanceTransferCreateRequest
{
    SourceAccountGroupId = new Guid("00000672-0000-0000-0000-000000000000"),
    TargetAccountGroupId = new Guid("000022de-0000-0000-0000-000000000000"),
    Amount = "amount2",
    Currency = Currency.Eur,
};
```

