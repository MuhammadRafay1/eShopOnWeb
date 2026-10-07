
# Meta 27

*This model accepts additional fields of type object.*

## Structure

`Meta27`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Offset` | `int` | Required | Amount of resource to offset in the response. |
| `Limit` | `int` | Required | Total limit of the response. |
| `Count` | `int` | Required | Count of the resources returned in the response. |
| `TotalCount` | `int` | Required | Total count of all the resources. |
| `Sort` | `string` | Required | The field that the list is sorted by. |
| `Order` | [`Order1`](../../doc/models/order-1.md) | Required | The ordering of the response.<br><br>* ASC - Ascending order<br>* DESC - Descending order |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Meta27 meta27 = new Meta27
{
    Offset = 70,
    Limit = 230,
    Count = 118,
    TotalCount = 46,
    Sort = "sort6",
    Order = Order1.Asc,
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

