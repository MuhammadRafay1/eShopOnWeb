
# Initiation Flow

Identifies what triggered the order.

* API — initiated directly via the client API.
* PORTFOLIO — initiated by a portfolio rebalancing flow.
* CASH_DIVIDEND_REINVESTMENT — initiated as part of dividend reinvestment.
* PORTFOLIO_REBALANCING — initiated by an automated rebalancing.
* SELL_TO_COVER_FEES — initiated automatically to cover outstanding fees.
* SELL_TO_COVER_TAXES — initiated automatically to cover tax obligations.
* ACCOUNT_LIQUIDATION — initiated as part of an account liquidation.
* UPVEST_OPERATIONS — initiated by Upvest operations.
* SAVINGS_PLAN — initiated by a savings plan execution.
* CLIENT_OPERATIONS — initiated by client operations.

## Enumeration

`InitiationFlow`

## Fields

| Name |
|  --- |
| `Api` |
| `Portfolio` |
| `CashDividendReinvestment` |
| `PortfolioRebalancing` |
| `SellToCoverFees` |
| `SellToCoverTaxes` |
| `AccountLiquidation` |
| `UpvestOperations` |
| `SavingsPlan` |
| `ClientOperations` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

InitiationFlow initiationFlow = InitiationFlow.Api;
```

