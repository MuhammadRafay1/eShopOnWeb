
# Type 50

Type of the reference.

* ACCOUNT_GROUP - Account group
* CASH_BALANCE_TRANSFER - Cash balance transfer
* CORPORATE_ACTION - Corporate action
* CORPORATE_ACTION_TRANSACTION_ID - Corporate action transaction ID
* CREDIT_FUNDING - Credit Funding
* DIRECT_DEBIT - Direct debit funding request
* EXTERNAL_DIRECT_DEBIT - External direct debit collection
* FEE_COLLECTION - Fee collection
* ISA_TRANSFER - ISA transfer
* ORDER - Order
* ORDER_EXECUTION - Order execution
* TAX_TRANSACTION - Tax transaction
* TOPUP - Cash top up
* TRANSACTION_FEE_EXECUTION - Transaction fee execution
* VIRTUAL_CASH_DECREASE - Virtual cash decreased
* VIRTUAL_CASH_INCREASE - Virtual cash increased
* WITHDRAWAL - Cash withdrawal

## Enumeration

`Type50`

## Fields

| Name |
|  --- |
| `AccountGroup` |
| `CashBalanceTransfer` |
| `CorporateAction` |
| `CorporateActionTransactionId` |
| `CreditFunding` |
| `DirectDebit` |
| `ExternalDirectDebit` |
| `FeeCollection` |
| `IsaTransfer` |
| `Order` |
| `OrderExecution` |
| `TaxTransaction` |
| `Topup` |
| `TransactionFeeExecution` |
| `VirtualCashDecrease` |
| `VirtualCashIncrease` |
| `Withdrawal` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Type50 type50 = Type50.IsaTransfer;
```

