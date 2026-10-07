
# Counterparty Agent

## Structure

`CounterpartyAgent`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Bic` | `string` | Required | Business Identifier Code (also known as SWIFT-BIC, BIC, SWIFT ID or SWIFT code) [ISO 9362](https://en.wikipedia.org/wiki/ISO_9362).<br><br>**Constraints**: *Pattern*: `^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

CounterpartyAgent counterpartyAgent = new CounterpartyAgent
{
    Bic = "bic8",
};
```

