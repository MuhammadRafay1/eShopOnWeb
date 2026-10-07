
# Payments Withdrawal Create Request

Request body for initiating a cash withdrawal from an account group to a registered reference bank account.

## Structure

`PaymentsWithdrawalCreateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `ReferenceAccountId` | `Guid` | Required | Reference account unique identifier. |
| `AccountGroupId` | `Guid` | Required | Account group unique identifier. |
| `UserId` | `Guid?` | Optional | The ID of the user. This field is optional and will be removed in a future version. |
| `Amount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | [`Currency?`](../../doc/models/currency.md) | Optional | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |
| `RemittanceInformation` | `string` | Optional | Payment reference the end user will see in their bank statement for the corresponding credit transfer booking (“Verwendungszweck”). We recommend that you keep this info concise and avoid special characters or non-standardised formatting. See the ([Cash balance withdrawal guide](/documentation/guides/payments/cash_balances/cash_withdrawal)).<br><br>**Constraints**: *Maximum Length*: `140`, *Pattern*: `^[0-9A-Za-z+?/\-:()\.,';_ ]{0,140}$` |
| `PurposeCode` | `string` | Optional | Purpose of the payment based on *ExternalPurpose1Code* from [ISO 20022](https://www.iso20022.org/catalogue-messages/additional-content-messages/external-code-sets).<br><br>**Constraints**: *Maximum Length*: `4` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

PaymentsWithdrawalCreateRequest paymentsWithdrawalCreateRequest = new PaymentsWithdrawalCreateRequest
{
    ReferenceAccountId = new Guid("00001e0e-0000-0000-0000-000000000000"),
    AccountGroupId = new Guid("0000063c-0000-0000-0000-000000000000"),
    Amount = "amount8",
    UserId = new Guid("000021e2-0000-0000-0000-000000000000"),
    Currency = Currency.Eur,
    RemittanceInformation = "remittance_information2",
    PurposeCode = "purpose_code2",
};
```

