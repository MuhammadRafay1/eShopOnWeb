
# Portfolios Order 1

An individual instrument order within a portfolio order, representing the BUY or SELL of a single instrument.

## Structure

`PortfoliosOrder1`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Unique identifier for an order. Universally Unique Identifier (UUID). |
| `Side` | [`Side15`](../../doc/models/side-15.md) | Required | Side of the portfolio order.<br><br>* BUY -<br>* SELL - |
| `Status` | [`Status51`](../../doc/models/status-51.md) | Required | The execution status of the order.<br><br>* NEW — the order has been received and validated, awaiting routing.<br>* PROCESSING — the order is being routed for execution.<br>* FILLED — the order has been fully executed.<br>* CANCELLED — the order was cancelled before being fully executed. |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

PortfoliosOrder1 portfoliosOrder1 = new PortfoliosOrder1
{
    Id = new Guid("00001288-0000-0000-0000-000000000000"),
    Side = Side15.Buy,
    Status = Status51.New,
};
```

