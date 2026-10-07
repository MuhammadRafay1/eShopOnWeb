
# Business Check Create Response

Response containing the unique identifier of the business check created.

## Structure

`BusinessCheckCreateResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Business Check unique identifier. |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

BusinessCheckCreateResponse businessCheckCreateResponse = new BusinessCheckCreateResponse
{
    Id = new Guid("000026fa-0000-0000-0000-000000000000"),
};
```

