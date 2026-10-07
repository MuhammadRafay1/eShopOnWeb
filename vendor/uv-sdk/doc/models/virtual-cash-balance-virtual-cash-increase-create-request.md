
# Virtual Cash Balance Virtual Cash Increase Create Request

## Structure

`VirtualCashBalanceVirtualCashIncreaseCreateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `AccountGroupId` | `Guid` | Required | Account group unique identifier. |
| `Amount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | [`Currency1`](../../doc/models/currency-1.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling<br>* USD - The United States dollar |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

VirtualCashBalanceVirtualCashIncreaseCreateRequest virtualCashBalanceVirtualCashIncreaseCreateRequest = new VirtualCashBalanceVirtualCashIncreaseCreateRequest
{
    AccountGroupId = new Guid("0000182e-0000-0000-0000-000000000000"),
    Amount = "amount2",
    Currency = Currency1.Eur,
};
```

