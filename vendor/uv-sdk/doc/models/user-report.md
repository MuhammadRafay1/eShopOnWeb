
# User Report

## Structure

`UserReport`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Report unique identifier. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UserId` | `Guid` | Required | Unique identifier of the user, as a UUID. |
| `Type` | [`ReportType`](../../doc/models/report-type.md) | Required | Report type<br><br>* ACCOUNT_CLOSING - Securities account closure<br>* ACCOUNT_OPENING - Securities account opening<br>* AD_HOC_ACCOUNT_STATEMENT - Ad hoc account statement<br>* ANNUAL_INCOME_STATEMENT - Annual tax income statement ("Erträgnisaufstellung")<br>* ANNUAL_TAX_STATEMENT - Annual tax reporting<br>* BANK_ACCOUNT_CONNECTED - Connect reference bank account<br>* BUY_ORDER - Buy order<br>* CANCELLED_ORDER - Cancelled buy or sell order (not yet executed, user cancels or trading venue cancelled)<br>* CORPORATE_ACTION_CASH_TRANSACTION - cash transaction document (after corporate action)<br>* CORPORATE_ACTION_SECURITY_TRANSACTION - security transaction document (after corporate action)<br>* DIRECT_DEBIT_MANDATE - Creation of SEPA direct debit mandate<br>* EX_POST_COST - Ex-post cost report<br>* FEE_COLLECTION - Intake of service fees<br>* FRENCH_IFU - French tax statement (Imprimé fiscal unique)<br>* GENERIC_COMMUNICATION - Generic communication<br>* INCOME_DISTRIBUTION - Income distribution report<br>* LOSS_THRESHOLD - Notification that a holding has depreciated by 10%, or a multiple thereof, against its purchase value<br>* MONTHLY_BILLING_STATEMENT - Monthly billing activities statement<br>* ORDER_EX_ANTE_COST - Ex-ante cost report for a single order<br>* ORDER_EX_ANTE_COST_SAVINGS_PLAN - Ex-ante cost report for a savings plan order<br>* QUARTERLY_ACCOUNT_STATEMENT - Quarterly account statement<br>* REVOKED_ORDER - Revoked buy or sell order ("Storno")<br>* SECURITIES_TRANSFER_INCOMING - Securities are transferred in<br>* SECURITIES_TRANSFER_OUTGOING - Securities are being transferred out<br>* SELL_ORDER - Sell order<br>* TAX_CONSOLIDATED_CERTIFICATE_UK - UK Consolidated Tax Certificate<br>* TAX_PAYMENT - Tax payment document<br>* TAX_PREPAYMENT_DE - German tax prepayment (Vorabpauschale)<br>* TAX_PREPAYMENT_DE_CANCELLATION - German tax prepayment (Vorabpauschale) cancellation<br>* TAX_REFUND - Tax refund document (after tax optimization) |
| `SubstitutedReportId` | `Guid?` | Required | - |
| `Data` | [`ReportData`](../../doc/models/report-data.md) | Optional | Contents of the report. |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

UserReport userReport = new UserReport
{
    Id = new Guid("00000da0-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UserId = new Guid("000014b0-0000-0000-0000-000000000000"),
    Type = ReportType.SecuritiesTransferIncoming,
    SubstitutedReportId = new Guid("00001d48-0000-0000-0000-000000000000"),
    Data = new ReportData
    {
        Account = new Account7
        {
            Id = new Guid("000025e4-0000-0000-0000-000000000000"),
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
        References = new List<ReportReferenceData>
        {
            new ReportReferenceData
            {
                Id = new Guid("00000f98-0000-0000-0000-000000000000"),
                Type = ReportReferenceType.AccountId,
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            },
        },
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
};
```

