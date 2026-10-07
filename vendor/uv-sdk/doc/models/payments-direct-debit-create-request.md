
# Payments Direct Debit Create Request

Request body for initiating a SEPA Direct Debit. Requires a valid `mandate_id` and the `account_group_id` of the account group to receive the funds.

## Structure

`PaymentsDirectDebitCreateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `UserId` | `Guid?` | Optional | The ID of the user. This field is optional and will be removed in a future version. |
| `AccountGroupId` | `Guid` | Required | Account group unique identifier. |
| `MandateId` | `Guid` | Required | Direct Debit Mandate unique identifier. |
| `CashAmount` | `string` | Required | **Constraints**: *Pattern*: `^-?[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | [`Currency?`](../../doc/models/currency.md) | Optional | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |
| `RemittanceInformation` | `string` | Optional | Payment reference the end user will see in their bank statement for the corresponding direct debit booking (“Verwendungszweck”). We recommend that you keep this info concise and avoid special characters or non-standardised formatting ([see more](/documentation/guides/payments/direct_debit/direct_debit_submitting)).<br><br>**Constraints**: *Maximum Length*: `140`, *Pattern*: `^[0-9A-Za-z+?/\-:()\.,';_ ]{0,140}$` |
| `PurposeCode` | `string` | Optional | Purpose of the payment based on *ExternalPurpose1Code* from [ISO 20022](https://www.iso20022.org/catalogue-messages/additional-content-messages/external-code-sets).<br><br>**Constraints**: *Maximum Length*: `4` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

PaymentsDirectDebitCreateRequest paymentsDirectDebitCreateRequest = new PaymentsDirectDebitCreateRequest
{
    AccountGroupId = new Guid("000000ce-0000-0000-0000-000000000000"),
    MandateId = new Guid("000008c0-0000-0000-0000-000000000000"),
    CashAmount = "cash_amount2",
    UserId = new Guid("00001ad8-0000-0000-0000-000000000000"),
    Currency = Currency.Eur,
    RemittanceInformation = "remittance_information0",
    PurposeCode = "purpose_code0",
};
```

