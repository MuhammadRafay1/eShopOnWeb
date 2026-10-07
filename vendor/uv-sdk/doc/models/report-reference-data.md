
# Report Reference Data

Reference data for a report

*This model accepts additional fields of type object.*

## Structure

`ReportReferenceData`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid?` | Optional | - |
| `Type` | [`ReportReferenceType?`](../../doc/models/report-reference-type.md) | Optional | Report reference type<br><br>* CORPORATE_ACTION_TRANSACTION_ID - Corporate action transaction identifier<br>* ACCOUNT_GROUP_ID - Account group identifier<br>* ACCOUNT_ID - Account identifier<br>* ORDER_ID - Order identifier |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

ReportReferenceData reportReferenceData = new ReportReferenceData
{
    Id = new Guid("0000120e-0000-0000-0000-000000000000"),
    Type = ReportReferenceType.CorporateActionTransactionId,
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

