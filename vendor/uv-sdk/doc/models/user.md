
# User

*This model accepts additional fields of type object.*

## Structure

`User`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid?` | Optional | Unique identifier of the user, as a UUID. |
| `Type` | [`Type12?`](../../doc/models/type-12.md) | Optional | Relation type<br><br>* OWNER - Account Group Owner<br>* CHILD - Child Account Group Owner<br>* GUARDIAN - Child Account Group Guardian |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

User user = new User
{
    Id = new Guid("0000143c-0000-0000-0000-000000000000"),
    Type = Type12.Guardian,
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

