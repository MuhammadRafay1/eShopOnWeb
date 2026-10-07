
# Payments Withdrawal

Represents a cash withdrawal from an account group to a registered reference bank account. Withdrawals transition through `NEW` → `PROCESSING` → `CONFIRMED` or `CANCELLED` as they are processed.

## Structure

`PaymentsWithdrawal`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Cash withdrawal unique identifier |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `ReferenceAccountId` | `Guid` | Required | Reference account unique identifier. |
| `AccountGroupId` | `Guid` | Required | Account group unique identifier. |
| `UserId` | `Guid?` | Optional | The ID of the user. For business-initiated withdrawals, this field will be absent. |
| `Amount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | [`Currency`](../../doc/models/currency.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |
| `RemittanceInformation` | `string` | Required | Payment reference the end user will see in their bank statement for the corresponding credit transfer booking (“Verwendungszweck”). We recommend that you keep this info concise and avoid special characters or non-standardised formatting. See the ([Cash balance withdrawal guide](/documentation/guides/payments/cash_balances/cash_withdrawal)).<br><br>**Constraints**: *Maximum Length*: `140`, *Pattern*: `^[0-9A-Za-z+?/\-:()\.,';_ ]{0,140}$` |
| `Taxes` | [`List<Tax>`](../../doc/models/tax.md) | Optional | - |
| `Status` | [`Status34?`](../../doc/models/status-34.md) | Optional | Status of the withdrawal<br><br>* NEW - Withdrawal is created but not started processing.<br>* PROCESSING - Withdrawal is in processing.<br>* CONFIRMED - Withdrawal was successfully sent to the bank for processing.<br>* CANCELLED - Withdrawal was cancelled. **Note**: In rare instances, a bank may reject a withdrawal after it has been confirmed.<br>  For more information, see the [Potential cancellation after confirmation](/documentation/guides/payments/cash_balances/cash_withdrawal#potential-cancellation-after-confirmation) section of the guide. |
| `CancellationReason` | [`CancellationReasonCodeForWithdrawal?`](../../doc/models/cancellation-reason-code-for-withdrawal.md) | Optional | Reason the withdrawal was cancelled. The field is present in case the withdrawal has a status of CANCELLED.<br><br>* CANCELLED_BY_BANK - The payment was not completed by your bank. Please check your account details or contact support.<br>* CANCELLED_BY_UPVEST - The payment was cancelled. Contact support for details.<br>* CANCELLED_BY_CLIENT - The payment was cancelled at your request.<br>* OTHER - Reason not available (applies to historical data only). |
| `PurposeCode` | `string` | Optional | Purpose of the payment based on *ExternalPurpose1Code* from [ISO 20022](https://www.iso20022.org/catalogue-messages/additional-content-messages/external-code-sets).<br><br>**Constraints**: *Maximum Length*: `4` |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

PaymentsWithdrawal paymentsWithdrawal = new PaymentsWithdrawal
{
    Id = new Guid("00000ba0-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    ReferenceAccountId = new Guid("00000edc-0000-0000-0000-000000000000"),
    AccountGroupId = new Guid("00001e1a-0000-0000-0000-000000000000"),
    Amount = "amount8",
    Currency = Currency.Eur,
    RemittanceInformation = "remittance_information2",
    UserId = new Guid("000012b0-0000-0000-0000-000000000000"),
    Taxes = new List<Tax>
    {
        new Tax
        {
            Amount = "amount2",
            Currency = Currency.Eur,
            Type = "type0",
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
        new Tax
        {
            Amount = "amount2",
            Currency = Currency.Eur,
            Type = "type0",
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
        new Tax
        {
            Amount = "amount2",
            Currency = Currency.Eur,
            Type = "type0",
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
    },
    Status = Status34.New,
    CancellationReason = CancellationReasonCodeForWithdrawal.CancelledByBank,
    PurposeCode = "purpose_code2",
};
```

