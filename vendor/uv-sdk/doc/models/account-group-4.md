
# Account Group 4

Account group details.

*This model accepts additional fields of type object.*

## Structure

`AccountGroup4`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid?` | Optional | Account group unique identifier. |
| `SecuritiesAccountNumber` | `string` | Optional | Securities account number.<br><br>**Constraints**: *Pattern*: `^[0-9]{9}$` |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

AccountGroup4 accountGroup4 = new AccountGroup4
{
    Id = new Guid("000007ae-0000-0000-0000-000000000000"),
    SecuritiesAccountNumber = "securities_account_number6",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

