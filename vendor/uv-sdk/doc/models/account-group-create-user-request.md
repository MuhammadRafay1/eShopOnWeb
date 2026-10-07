
# Account Group Create User Request

*This model accepts additional fields of type object.*

## Structure

`AccountGroupCreateUserRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `UserId` | `Guid` | Required | Unique identifier of the user, as a UUID. |
| `Type` | [`Type13`](../../doc/models/type-13.md) | Required | Account group type.<br><br>* PERSONAL - Account group of a person holding assets on their own behalf.<br>* LEGAL_ENTITY - Account group of a legal entity holding assets on behalf of their users.<br>* FRENCH_PEA - Account group of a french resident holding assets in Plan d'Epargne en Actions.<br>* ISA - Account group of a UK resident holding assets in an individual savings account.<br>* CHILD - Account group of a child user holding assets in a child account. |
| `SecuritiesAccountNumber` | `string` | Optional | Account unique identifier. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

AccountGroupCreateUserRequest accountGroupCreateUserRequest = new AccountGroupCreateUserRequest
{
    UserId = new Guid("00000ae4-0000-0000-0000-000000000000"),
    Type = Type13.Personal,
    SecuritiesAccountNumber = "securities_account_number0",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

