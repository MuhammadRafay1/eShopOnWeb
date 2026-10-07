
# Fx

Entity representing the applied FX rate for a cash transaction. Present only for corporate action cash transactions where the distribution currency differs from the source/income currency. Omitted for every other transaction type, and also for domestic transactions where those currencies match (no conversion occurred).

## Structure

`Fx`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `BaseCurrency` | [`BaseCurrency`](../../doc/models/base-currency.md) | Required | The distribution currency. |
| `QuoteCurrency` | [`QuoteCurrency`](../../doc/models/quote-currency.md) | Required | The source/income currency. |
| `Rate` | [`FxRate`](../../doc/models/fx-rate.md) | Required | Entity representing the applied FX rate for a currency conversion. |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Fx fx = new Fx
{
    BaseCurrency = BaseCurrency.Gbp,
    QuoteCurrency = QuoteCurrency.Eur,
    Rate = new FxRate
    {
        AllInRate = "all_in_rate8",
        BaseRate = "base_rate8",
        MarkupRate = "markup_rate8",
    },
};
```

