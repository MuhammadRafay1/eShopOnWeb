
# Payments Bank Transaction Create Request

Request body for recording an incoming bank transaction against a virtual bank account.

## Structure

`PaymentsBankTransactionCreateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Account` | [`Account15`](../../doc/models/account-15.md) | Required | - |
| `Amount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | [`BankTransactionCurrency`](../../doc/models/bank-transaction-currency.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code |
| `RemittanceInformation` | `string` | Optional | Information supplied to enable the matching/reconciliation of an entry with the items that the payment is intended to settle, such as commercial invoices in an accounts' receivable system, in an unstructured form.<br><br>**Constraints**: *Maximum Length*: `140`, *Pattern*: `^[0-9A-Za-z+?/\-:()\.,'; ]{0,140}$` |
| `ClientReference` | `string` | Required | Immutable reference to the API flow that initiated the order. For client initiated API flows, this is a client provided ID. For internal initiations, it is set to the ID of the related object.<br><br>**Constraints**: *Maximum Length*: `35`, *Pattern*: `^[0-9A-Za-z+?/\-:()\.,'; ]{0,35}$` |
| `CounterpartyName` | `string` | Required | The counterparty account owner<br><br>**Constraints**: *Maximum Length*: `140`, *Pattern*: `^[0-9A-Za-z+?/\-:()\.,'; ]{0,140}$` |
| `CounterpartyAccount` | [`CounterpartyAccount`](../../doc/models/counterparty-account.md) | Required | - |
| `CounterpartyAgent` | [`CounterpartyAgent`](../../doc/models/counterparty-agent.md) | Optional | - |
| `VirtualBankAccountId` | `Guid?` | Optional | - |
| `PurposeCode` | `string` | Optional | An optional parameter, which describes the purpose of the payment based on *ExternalPurpose1Code* from [ISO 20022](https://www.iso20022.org/catalogue-messages/additional-content-messages/external-code-sets).<br><br>**Constraints**: *Maximum Length*: `4` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

PaymentsBankTransactionCreateRequest paymentsBankTransactionCreateRequest = new PaymentsBankTransactionCreateRequest
{
    Account = new Account15
    {
        Iban = "iban4",
    },
    Amount = "amount8",
    Currency = BankTransactionCurrency.Gbp,
    ClientReference = "client_reference8",
    CounterpartyName = "counterparty_name0",
    CounterpartyAccount = new CounterpartyAccount
    {
        Iban = "iban4",
    },
    RemittanceInformation = "remittance_information2",
    CounterpartyAgent = new CounterpartyAgent
    {
        Bic = "bic4",
    },
    VirtualBankAccountId = new Guid("000026c6-0000-0000-0000-000000000000"),
    PurposeCode = "purpose_code2",
};
```

