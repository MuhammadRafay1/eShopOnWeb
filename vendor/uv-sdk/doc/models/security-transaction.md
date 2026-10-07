
# Security Transaction

*This model accepts additional fields of type object.*

## Structure

`SecurityTransaction`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `AccountGroupId` | `Guid` | Required | Account group unique identifier. |
| `AccountId` | `Guid` | Required | Account unique identifier. |
| `BookingDate` | `DateTime` | Required | Transaction booking date and time. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `Delta` | [`SecurityTransactionDelta`](../../doc/models/security-transaction-delta.md) | Required | Entity representing the security transaction delta. |
| `Id` | `Guid` | Required | Security transaction unique identifier. |
| `Instrument` | [`Instrument8`](../../doc/models/instrument-8.md) | Required | Entity representing instrument. |
| `Type` | [`TransactionType1`](../../doc/models/transaction-type-1.md) | Required | Transaction type<br><br>* ACCUMULATION - Accumulation<br>* ACCUMULATION_CANCELLATION - Accumulation cancellation<br>* ANNUAL_GENERAL_MEETING - Annual general meeting<br>* ANNUAL_GENERAL_MEETING_CANCELLATION - Annual general meeting cancellation<br>* ATTACHMENT - Attachment<br>* ATTACHMENT_CANCELLATION - Attachment cancellation<br>* BALANCE_CORRECTION - Balance correction<br>* BALANCE_CORRECTION_CANCELLATION - Balance correction cancellation<br>* BANKRUPTCY - Bankruptcy<br>* BANKRUPTCY_CANCELLATION - Bankruptcy cancellation<br>* BOND_DEFAULT - Bond default<br>* BOND_DEFAULT_CANCELLATION - Bond default cancellation<br>* BOND_HOLDER_MEETING - Bond holder meeting<br>* BOND_HOLDER_MEETING_CANCELLATION - Bond holder meeting cancellation<br>* BONUS_ISSUE - Bonus Issue/Capitalisation Issue<br>* BONUS_ISSUE_CANCELLATION - Bonus Issue/Capitalisation Issue, Cancellation<br>* CALL_ON_INTERMEDIATE_SECURITIES - Call on Intermediate Securities<br>* CALL_ON_INTERMEDIATE_SECURITIES_CANCELLATION - Call on Intermediate Securities, Cancellation<br>* CAPITAL_DISTRIBUTION - Capital distribution<br>* CAPITAL_DISTRIBUTION_CANCELLATION - Capital distribution cancellation<br>* CAPITAL_GAINS_DISTRIBUTION - Capital gains distribution<br>* CAPITAL_GAINS_DISTRIBUTION_CANCELLATION - Capital gains distribution cancellation<br>* CAPITALISATION - Capitalisation<br>* CAPITALISATION_CANCELLATION - Capitalisation cancellation<br>* CASH_DISTRIBUTION_FROM_NON_ELIGIBLE_SECURITIES_SALES - Cash distribution from non eligible securities sales<br>* CASH_DISTRIBUTION_FROM_NON_ELIGIBLE_SECURITIES_SALES_CANCELLATION - Cash distribution from non eligible securities sales cancellation<br>* CASH_DIVIDEND - Cash dividend<br>* CASH_DIVIDEND_CANCELLATION - Cash dividend cancellation<br>* CHANGE - Change<br>* CHANGE_CANCELLATION - Change, Cancellation<br>* CLASS_ACTION - Class action<br>* CLASS_ACTION_CANCELLATION - Class action cancellation<br>* COMPANY_OPTION - Company option<br>* COMPANY_OPTION_CANCELLATION - Company option cancellation<br>* CONSENT - Consent<br>* CONSENT_CANCELLATION - Consent cancellation<br>* CONVERSION - Conversion<br>* CONVERSION_CANCELLATION - Conversion cancellation<br>* COURT_MEETING - Court meeting<br>* COURT_MEETING_CANCELLATION - Court meeting cancellation<br>* CREDIT_EVENT - Credit event<br>* CREDIT_EVENT_CANCELLATION - Credit event cancellation<br>* DECREASE_IN_VALUE - Decrease in value<br>* DECREASE_IN_VALUE_CANCELLATION - Decrease in value cancellation<br>* DETACHMENT - Detachment<br>* DETACHMENT_CANCELLATION - Detachment cancellation<br>* DISCLOSURE - Disclosure<br>* DISCLOSURE_CANCELLATION - Disclosure cancellation<br>* DIVIDEND_OPTION - Dividend Option<br>* DIVIDEND_OPTION_CANCELLATION - Dividend Option, Cancellation<br>* DIVIDEND_REINVESTMENT - Dividend Reinvestment<br>* DIVIDEND_REINVESTMENT_CANCELLATION - Dividend Reinvestment, Cancellation<br>* DRAWING - Drawing<br>* DRAWING_CANCELLATION - Drawing cancellation<br>* DUST_CLEANUP - Dust Cleanup<br>* DUTCH_AUCTION - Dutch Auction<br>* DUTCH_AUCTION_CANCELLATION - Dutch Auction, Cancellation<br>* EXCHANGE - Exchange<br>* EXCHANGE_CANCELLATION - Exchange cancellation<br>* EXTRAORDINARY_OR_SPECIAL_GENERAL_MEETING - Extraordinary or special general meeting<br>* EXTRAORDINARY_OR_SPECIAL_GENERAL_MEETING_CANCELLATION - Extraordinary or special general meeting cancellation<br>* FINAL_MATURITY - Final maturity<br>* FINAL_MATURITY_CANCELLATION - Final maturity cancellation<br>* FULL_CALL - Full call<br>* FULL_CALL_CANCELLATION - Full call cancellation<br>* INCREASE_IN_VALUE - Increase in value<br>* INCREASE_IN_VALUE_CANCELLATION - Increase in value cancellation<br>* INFORMATION - Information<br>* INFORMATION_CANCELLATION - Information cancellation<br>* INSTALMENT_CALL - Instalment call<br>* INSTALMENT_CALL_CANCELLATION - Instalment call cancellation<br>* INTEREST_PAYMENT - Interest payment<br>* INTEREST_PAYMENT_CANCELLATION - Interest payment cancellation<br>* INTERMEDIATE_SECURITIES_DISTRIBUTION - Intermediate Securities Distribution<br>* INTERMEDIATE_SECURITIES_DISTRIBUTION_CANCELLATION - Intermediate Securities Distribution, Cancellation<br>* LIQUIDATION_PAYMENT - Liquidation Dividend/Liquidation Payment<br>* LIQUIDATION_PAYMENT_CANCELLATION - Liquidation Dividend/Liquidation Payment, Cancellation<br>* MATURITY_EXTENSION - Maturity extension<br>* MATURITY_EXTENSION_CANCELLATION - Maturity extension cancellation<br>* MERGER - Merger<br>* MERGER_CANCELLATION - Merger, Cancellation<br>* NON_OFFICIAL_OFFER - Non-Official Offer<br>* NON_OFFICIAL_OFFER_CANCELLATION - Non-Official Offer, Cancellation<br>* NON_US_TEFRA_D_CERTIFICATION - Non us tefra d certification<br>* NON_US_TEFRA_D_CERTIFICATION_CANCELLATION - Non us tefra d certification cancellation<br>* ODD_LOT_SALE - Odd Lot Sale/Purchase<br>* ODD_LOT_SALE_CANCELLATION - Odd Lot Sale/Purchase, Cancellation<br>* ORDER_EXECUTION - Order execution<br>* ORDER_EXECUTION_CANCELLATION - Order execution cancellation<br>* ORDINARY_GENERAL_MEETING - Ordinary general meeting<br>* ORDINARY_GENERAL_MEETING_CANCELLATION - Ordinary general meeting cancellation<br>* OTHER_EVENT - Other Event<br>* OTHER_EVENT_CANCELLATION - Other Event cancellation<br>* PARI_PASSU - Pari passu<br>* PARI_PASSU_CANCELLATION - Pari passu cancellation<br>* PARTIAL_DEFEASANCE - Partial defeasance<br>* PARTIAL_DEFEASANCE_CANCELLATION - Partial defeasance cancellation<br>* PARTIAL_REDEMPTION_WITH_POOL_FACTOR_REDUCTION - Partial redemption with pool factor reduction<br>* PARTIAL_REDEMPTION_WITH_POOL_FACTOR_REDUCTION_CANCELLATION - Partial redemption with pool factor reduction cancellation<br>* PARTIAL_REDEMPTION_WITHOUT_POOL_FACTOR_REDUCTION - Partial redemption without pool factor reduction<br>* PARTIAL_REDEMPTION_WITHOUT_POOL_FACTOR_REDUCTION_CANCELLATION - Partial redemption without pool factor reduction cancellation<br>* PAY_IN_KIND - Pay in kind<br>* PAY_IN_KIND_CANCELLATION - Pay in kind cancellation<br>* PEAK_FRACTION_COMPENSATION - Peak fraction compensation<br>* PEAK_ORDER_ADJUSTMENT - Peak order adjustment<br>* PLACE_OF_INCORPORATION - Place of incorporation<br>* PLACE_OF_INCORPORATION_CANCELLATION - Place of incorporation cancellation<br>* PRIORITY_ISSUE - Priority Issue<br>* PRIORITY_ISSUE_CANCELLATION - Priority Issue, Cancellation<br>* PUT_REDEMPTION - Put redemption<br>* PUT_REDEMPTION_CANCELLATION - Put redemption cancellation<br>* REDENOMINATION - Redenomination<br>* REDENOMINATION_CANCELLATION - Redenomination cancellation<br>* REMARKETING_AGREEMENT - Remarketing agreement<br>* REMARKETING_AGREEMENT_CANCELLATION - Remarketing agreement cancellation<br>* REPURCHASE_OFFER - Repurchase Offer/Issuer Bid/Reverse Rights<br>* REPURCHASE_OFFER_CANCELLATION - Repurchase Offer/Issuer Bid/Reverse Rights, Cancellation<br>* REVERSE_STOCK_SPLIT - Reverse Stock Split/Change in Nominal Value<br>* REVERSE_STOCK_SPLIT_CANCELLATION - Reverse Stock Split/Change in Nominal Value, Cancellation<br>* RIGHTS_ISSUE - Rights Issue/Subscription Rights/Rights Offer<br>* RIGHTS_ISSUE_CANCELLATION - Rights Issue/Subscription Rights/Rights Offer, Cancellation<br>* SCRIP_DIVIDEND - Scrip dividend<br>* SCRIP_DIVIDEND_CANCELLATION - Scrip dividend cancellation<br>* SECURITIES_TRANSFER - Securities transfer<br>* SECURITIES_TRANSFER_CANCELLATION - Securities transfer cancellation<br>* SHARES_PREMIUM_DIVIDEND - Shares premium dividend<br>* SHARES_PREMIUM_DIVIDEND_CANCELLATION - Shares premium dividend cancellation<br>* SMALLEST_NEGOTIABLE_UNIT - Smallest negotiable unit<br>* SMALLEST_NEGOTIABLE_UNIT_CANCELLATION - Smallest negotiable unit cancellation<br>* SPIN_OFF - Spin-Off<br>* SPIN_OFF_CANCELLATION - Spin-Off, Cancellation<br>* STOCK_DIVIDEND - Stock Dividend<br>* STOCK_DIVIDEND_CANCELLATION - Stock Dividend, Cancellation<br>* STOCK_SPLIT - Stock Split/Change in Nominal Value/Subdivision<br>* STOCK_SPLIT_CANCELLATION - Stock Split/Change in Nominal Value/Subdivision, Cancellation<br>* TAX_ON_NON_DISTRIBUTED_PROCEEDS - Tax on non distributed proceeds<br>* TAX_ON_NON_DISTRIBUTED_PROCEEDS_CANCELLATION - Tax on non distributed proceeds cancellation<br>* TAX_RECLAIM - Tax reclaim<br>* TAX_RECLAIM_CANCELLATION - Tax reclaim cancellation<br>* TENDER - Tender/Acquisition/Takeover/Purchase Offer<br>* TENDER_CANCELLATION - Tender/Acquisition/Takeover/Purchase Offer, Cancellation<br>* TRADING_STATUS_ACTIVE - Trading status active<br>* TRADING_STATUS_ACTIVE_CANCELLATION - Trading status active cancellation<br>* TRADING_STATUS_DELISTED - Trading status delisted<br>* TRADING_STATUS_DELISTED_CANCELLATION - Trading status delisted cancellation<br>* TRADING_STATUS_SUSPENDED - Trading status suspended<br>* TRADING_STATUS_SUSPENDED_CANCELLATION - Trading status suspended cancellation<br>* VIRTUAL_CASH_CORRECTION_CANCELLATION - Virtual cash correction cancellation<br>* WARRANT_EXERCISE - Warrant exercise<br>* WARRANT_EXERCISE_CANCELLATION - Warrant exercise cancellation<br>* WITHHOLDING_TAX_RELIEF_CERTIFICATION - Withholding tax relief certification<br>* WITHHOLDING_TAX_RELIEF_CERTIFICATION_CANCELLATION - Withholding tax relief certification cancellation<br>* WORTHLESS - Worthless<br>* WORTHLESS_CANCELLATION - Worthless, Cancellation<br>* SECURITIES_LENDING - Securities lending |
| `References` | [`List<SecurityTransactionReference>`](../../doc/models/security-transaction-reference.md) | Required | - |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `ValueDate` | `DateTime` | Required | Transaction value date and time. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

SecurityTransaction securityTransaction = new SecurityTransaction
{
    AccountGroupId = new Guid("000018ea-0000-0000-0000-000000000000"),
    AccountId = new Guid("00000014-0000-0000-0000-000000000000"),
    BookingDate = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    Delta = new SecurityTransactionDelta
    {
        Amount = "amount0",
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    Id = new Guid("00000670-0000-0000-0000-000000000000"),
    Instrument = new Instrument8
    {
        Uuid = new Guid("00001fd8-0000-0000-0000-000000000000"),
        Isin = "isin4",
    },
    Type = TransactionType1.CapitalisationCancellation,
    References = new List<SecurityTransactionReference>
    {
        new SecurityTransactionReference
        {
            Id = new Guid("00000f98-0000-0000-0000-000000000000"),
            Type = Type51.CorporateAction,
        },
    },
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    ValueDate = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

