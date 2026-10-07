
# Account Group Create Business Request

*This model accepts additional fields of type object.*

## Structure

`AccountGroupCreateBusinessRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `BusinessId` | `Guid` | Required | Unique identifier for the business. |
| `Type` | `string` | Required, Constant | Account group type.<br><br>* BUSINESS - Account group of a business holding assets.<br><br>**Value**: `"BUSINESS"` |
| `SecuritiesAccountNumber` | `string` | Optional | Account unique identifier. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

AccountGroupCreateBusinessRequest accountGroupCreateBusinessRequest = new AccountGroupCreateBusinessRequest
{
    BusinessId = new Guid("000000c4-0000-0000-0000-000000000000"),
    Type = "BUSINESS",
    SecuritiesAccountNumber = "securities_account_number6",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

