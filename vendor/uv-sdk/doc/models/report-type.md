
# Report Type

Report type

* ACCOUNT_CLOSING - Securities account closure
* ACCOUNT_OPENING - Securities account opening
* AD_HOC_ACCOUNT_STATEMENT - Ad hoc account statement
* ANNUAL_INCOME_STATEMENT - Annual tax income statement ("Erträgnisaufstellung")
* ANNUAL_TAX_STATEMENT - Annual tax reporting
* BANK_ACCOUNT_CONNECTED - Connect reference bank account
* BUY_ORDER - Buy order
* CANCELLED_ORDER - Cancelled buy or sell order (not yet executed, user cancels or trading venue cancelled)
* CORPORATE_ACTION_CASH_TRANSACTION - cash transaction document (after corporate action)
* CORPORATE_ACTION_SECURITY_TRANSACTION - security transaction document (after corporate action)
* DIRECT_DEBIT_MANDATE - Creation of SEPA direct debit mandate
* EX_POST_COST - Ex-post cost report
* FEE_COLLECTION - Intake of service fees
* FRENCH_IFU - French tax statement (Imprimé fiscal unique)
* GENERIC_COMMUNICATION - Generic communication
* INCOME_DISTRIBUTION - Income distribution report
* LOSS_THRESHOLD - Notification that a holding has depreciated by 10%, or a multiple thereof, against its purchase value
* MONTHLY_BILLING_STATEMENT - Monthly billing activities statement
* ORDER_EX_ANTE_COST - Ex-ante cost report for a single order
* ORDER_EX_ANTE_COST_SAVINGS_PLAN - Ex-ante cost report for a savings plan order
* QUARTERLY_ACCOUNT_STATEMENT - Quarterly account statement
* REVOKED_ORDER - Revoked buy or sell order ("Storno")
* SECURITIES_TRANSFER_INCOMING - Securities are transferred in
* SECURITIES_TRANSFER_OUTGOING - Securities are being transferred out
* SELL_ORDER - Sell order
* TAX_CONSOLIDATED_CERTIFICATE_UK - UK Consolidated Tax Certificate
* TAX_PAYMENT - Tax payment document
* TAX_PREPAYMENT_DE - German tax prepayment (Vorabpauschale)
* TAX_PREPAYMENT_DE_CANCELLATION - German tax prepayment (Vorabpauschale) cancellation
* TAX_REFUND - Tax refund document (after tax optimization)

## Enumeration

`ReportType`

## Fields

| Name |
|  --- |
| `AccountClosing` |
| `AccountOpening` |
| `AdHocAccountStatement` |
| `AnnualIncomeStatement` |
| `AnnualTaxStatement` |
| `BankAccountConnected` |
| `BuyOrder` |
| `CancelledOrder` |
| `CorporateActionCashTransaction` |
| `CorporateActionSecurityTransaction` |
| `DirectDebitMandate` |
| `ExPostCost` |
| `FeeCollection` |
| `FrenchIfu` |
| `GenericCommunication` |
| `IncomeDistribution` |
| `LossThreshold` |
| `MonthlyBillingStatement` |
| `OrderExAnteCost` |
| `OrderExAnteCostSavingsPlan` |
| `QuarterlyAccountStatement` |
| `RevokedOrder` |
| `SecuritiesTransferIncoming` |
| `SecuritiesTransferOutgoing` |
| `SellOrder` |
| `TaxConsolidatedCertificateUk` |
| `TaxPayment` |
| `TaxPrepaymentDe` |
| `TaxPrepaymentDeCancellation` |
| `TaxRefund` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

ReportType reportType = ReportType.LossThreshold;
```

