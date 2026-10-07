
# Fee Collection Create Request

## Structure

`FeeCollectionCreateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `AccountId` | `Guid` | Required | Account unique identifier. |
| `Type` | [`Type37`](../../doc/models/type-37.md) | Required | Type of the fee collection<br><br>* SERVICE_FEE - Service fee intake in a pre-defined cadence (e.g. monthly)<br>* SERVICE_FEE_LIQUIDATION - Service fee intake as a result of a Portfolio liquidation |
| `CollectionAmount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | [`Currency`](../../doc/models/currency.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |
| `PeriodStart` | `DateTime` | Required | Start date of the fee collection period in YYYY-MM-DD format. [RFC 3339, section 5.6](https://json-schema.org/draft/2020-12/json-schema-validation.html#RFC3339) RFC 3339 |
| `PeriodEnd` | `DateTime` | Required | End date of the fee collection period in YYYY-MM-DD format. [RFC 3339, section 5.6](https://json-schema.org/draft/2020-12/json-schema-validation.html#RFC3339) RFC 3339 |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

FeeCollectionCreateRequest feeCollectionCreateRequest = new FeeCollectionCreateRequest
{
    AccountId = new Guid("000025ea-0000-0000-0000-000000000000"),
    Type = Type37.ServiceFee,
    CollectionAmount = "collection_amount0",
    Currency = Currency.Eur,
    PeriodStart = DateTime.Parse("2016-03-13"),
    PeriodEnd = DateTime.Parse("2016-03-13"),
};
```

