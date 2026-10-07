
# Payments Bank Transaction Create Response

Response returned after recording an incoming bank transaction. Contains the internal transaction `id` and the bank's own `bank_reference`.

*This model accepts additional fields of type object.*

## Structure

`PaymentsBankTransactionCreateResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Bank transaction unique identifier |
| `BankReference` | `string` | Required | Bank reference of the bank transaction (created by the bank to unambiguously identify the bank transaction in the banks systems) |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

PaymentsBankTransactionCreateResponse paymentsBankTransactionCreateResponse = new PaymentsBankTransactionCreateResponse
{
    Id = new Guid("00000056-0000-0000-0000-000000000000"),
    BankReference = "bank_reference6",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

