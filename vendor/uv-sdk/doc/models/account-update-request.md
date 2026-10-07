
# Account Update Request

## Structure

`AccountUpdateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Name` | `string` | Optional | The name of the account.<br><br>**Constraints**: *Maximum Length*: `100` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

AccountUpdateRequest accountUpdateRequest = new AccountUpdateRequest
{
    Name = "name4",
};
```

