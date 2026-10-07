
# Cash Transaction Reference

Entity representing cash transaction reference.

## Structure

`CashTransactionReference`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Unique identifier for a resource of given type. |
| `Type` | [`Type50`](../../doc/models/type-50.md) | Required | Type of the reference.<br><br>* ACCOUNT_GROUP - Account group<br>* CASH_BALANCE_TRANSFER - Cash balance transfer<br>* CORPORATE_ACTION - Corporate action<br>* CORPORATE_ACTION_TRANSACTION_ID - Corporate action transaction ID<br>* CREDIT_FUNDING - Credit Funding<br>* DIRECT_DEBIT - Direct debit funding request<br>* EXTERNAL_DIRECT_DEBIT - External direct debit collection<br>* FEE_COLLECTION - Fee collection<br>* ISA_TRANSFER - ISA transfer<br>* ORDER - Order<br>* ORDER_EXECUTION - Order execution<br>* TAX_TRANSACTION - Tax transaction<br>* TOPUP - Cash top up<br>* TRANSACTION_FEE_EXECUTION - Transaction fee execution<br>* VIRTUAL_CASH_DECREASE - Virtual cash decreased<br>* VIRTUAL_CASH_INCREASE - Virtual cash increased<br>* WITHDRAWAL - Cash withdrawal |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

CashTransactionReference cashTransactionReference = new CashTransactionReference
{
    Id = new Guid("000002d6-0000-0000-0000-000000000000"),
    Type = Type50.CreditFunding,
};
```

