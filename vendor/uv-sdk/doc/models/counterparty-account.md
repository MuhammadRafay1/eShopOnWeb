
# Counterparty Account

## Structure

`CounterpartyAccount`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Iban` | `string` | Required | International Bank Account Number [IBAN](https://en.wikipedia.org/wiki/International_Bank_Account_Number).<br><br>**Constraints**: *Pattern*: `^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,26}$` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

CounterpartyAccount counterpartyAccount = new CounterpartyAccount
{
    Iban = "iban4",
};
```

