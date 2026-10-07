
# User Check Create Response

Response containing the unique identifier of the user check created.

## Structure

`UserCheckCreateResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | User Check unique identifier. |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

UserCheckCreateResponse userCheckCreateResponse = new UserCheckCreateResponse
{
    Id = new Guid("0000045c-0000-0000-0000-000000000000"),
};
```

