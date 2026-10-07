
# Datum 2

Represents a SEPA Direct Debit funding request for an account group. Debits are initiated via a mandate and transition through `NEW` → `PROCESSING` → `CONFIRMED` or `CANCELLED` as they are processed.

## Structure

`Datum2`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Direct debit funding request unique identifier |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UserId` | `Guid?` | Optional | The ID of the user. For business-initiated direct debits, this field will be absent. |
| `AccountGroupId` | `Guid` | Required | Account group unique identifier. |
| `MandateId` | `Guid` | Required | Direct Debit Mandate unique identifier. |
| `CashAmount` | `string` | Required | **Constraints**: *Pattern*: `^-?[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | [`Currency`](../../doc/models/currency.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |
| `RemittanceInformation` | `string` | Optional | Payment reference the end user will see in their bank statement for the corresponding direct debit booking (“Verwendungszweck”). We recommend that you keep this info concise and avoid special characters or non-standardised formatting ([see more](/documentation/guides/payments/direct_debit/direct_debit_submitting)).<br><br>**Constraints**: *Maximum Length*: `140`, *Pattern*: `^[0-9A-Za-z+?/\-:()\.,';_ ]{0,140}$` |
| `Status` | [`Status26?`](../../doc/models/status-26.md) | Optional | Status of the direct debit<br><br>* NEW - Direct debit is created but not started processing.<br>* PROCESSING - Direct debit is in processing.<br>* CONFIRMED - Direct debit was successfully processed.<br>* CANCELLED - Direct debit was cancelled. |
| `CancellationReason` | [`CancellationReasonCodeForDirectDebit?`](../../doc/models/cancellation-reason-code-for-direct-debit.md) | Optional | Reason the direct debit was cancelled. The field is present in case the direct debit has a status of CANCELLED.<br><br>* CANCELLED_BY_BANK - The payment was not completed by your bank. Please check your account details or contact support.<br>* CANCELLED_BY_UPVEST - The payment was cancelled. Contact support for details.<br>* CANCELLED_BY_CLIENT - The payment was cancelled at your request.<br>* OTHER - Reason not available (applies to historical data only). |
| `PurposeCode` | `string` | Optional | Purpose of the payment based on *ExternalPurpose1Code* from [ISO 20022](https://www.iso20022.org/catalogue-messages/additional-content-messages/external-code-sets).<br><br>**Constraints**: *Maximum Length*: `4` |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;

Datum2 datum2 = new Datum2
{
    Id = new Guid("00001a8c-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    AccountGroupId = new Guid("000005f6-0000-0000-0000-000000000000"),
    MandateId = new Guid("00000f84-0000-0000-0000-000000000000"),
    CashAmount = "cash_amount4",
    Currency = Currency.Eur,
    UserId = new Guid("0000219c-0000-0000-0000-000000000000"),
    RemittanceInformation = "remittance_information2",
    Status = Status26.New,
    CancellationReason = CancellationReasonCodeForDirectDebit.CancelledByBank,
    PurposeCode = "purpose_code2",
};
```

