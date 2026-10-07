
# Counterparty 2

## Structure

`Counterparty2`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `AccountNumber` | `string` | Required | The account number is composed of valid Swift charset with a max length of 28 if provided. The account number helps other brokers identify the owner of the assets.<br><br>**Constraints**: *Pattern*: `^[0-9A-Za-z+?/\-:()\.,' ]{0,28}$` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Counterparty2 counterparty2 = new Counterparty2
{
    AccountNumber = "account_number4",
};
```

