
# Oauth Scope

OAuth 2 scopes supported by the API

## Enumeration

`OauthScope`

## Fields

| Name | Description |
|  --- | --- |
| `Accountsread` | Read accounts and account groups |
| `Accountsadmin` | Create/update/delete accounts and account groups |
| `Webhooksadmin` | Create/update/delete webhooks |
| `Webhooksread` | Read webhooks |
| `Filesread` | Read files metadata |
| `Ordersadmin` | Create/update/delete orders |
| `Ordersread` | Read orders |
| `Usersadmin` | Create/update/delete users |
| `Usersread` | Read users |
| `Checksadmin` | Create checks |
| `Checksread` | Read checks |
| `Positionsread` | Read positions |
| `PositionsPerformanceread` | Read positions performance |
| `ReferenceAccountsadmin` | Create/update/delete reference accounts |
| `ReferenceAccountsread` | Read reference accounts |
| `Mandatesadmin` | Create/update/delete mandates |
| `Mandatesread` | Read mandates |
| `Paymentsadmin` | Payins and withdrawal operations |
| `Paymentsread` | Payins and withdrawal read operations |
| `Topupsadmin` | Top-ups operations |
| `Topupsread` | Top-ups read operations |
| `CashBalanceTransfersadmin` | Cash balance transfer operations |
| `CashBalanceTransfersread` | Cash balance transfer read operations |
| `CreditFundingsread` | Credit Fundings read operations |
| `SecuritiesTransfersread` | Securities Transfers read transfers |
| `SecuritiesTransfersadmin` | Securities Transfers operations |
| `IsaTransfersadmin` | ISA Transfers operations |
| `Reportsread` | Read reports |
| `Reportsadmin` | Create reports |
| `Taxesread` | Read tax residencies |
| `Taxesadmin` | Modify tax residencies and tax exemptions |
| `Transactionsread` | Read cash and securities transactions |
| `Instrumentsread` | Read instruments |
| `Feesadmin` | Create and read fee operations |
| `Feesread` | Read fee operations |
| `TransactionFeesadmin` | Create and read transaction fee operations |
| `TransactionFeesread` | Read transaction fee operations |
| `Portfoliosread` | Read portfolios |
| `Portfoliosadmin` | Modify portfolios |
| `Valuationsread` | Read valuations |
| `AccountLiquidationsread` | Read accounts liquidations |
| `AccountLiquidationsadmin` | Trigger/read/cancel accounts liquidations |
| `AccountReturnsread` | Read accounts returns |
| `VirtualCashBalancesadmin` | Virtual cash balances |
| `SavingsPlansread` | Read savings plans |
| `SavingsPlansadmin` | Create/read savings plans |
| `Pricesread` | Read instrument prices, |
| `Testsadmin` | Testing related operations |
| `AccountTransfersread` | Account Transfers read transfers |
| `AccountTransfersadmin` | Account Transfers operations |
| `Businessesadmin` | Create/update/delete businesses, |
| `Businessesread` | Read businesses, |
| `Rolesadmin` | Create/update/deactivate roles |
| `Rolesread` | Read roles |
| `CorporateActionsadmin` | Create/update/delete corporate action instructions |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

OauthScope oauthScope = OauthScope.Businessesadmin;
```

