# Returns

All accounts returns related paths.

```csharp
ReturnsApi returnsApi = client.ReturnsApi;
```

## Class Name

`ReturnsApi`


# List Account Returns

List account returns

```csharp
ListAccountReturnsAsync(
    Guid accountId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    string startDate = null,
    string endDate = null,
    Models.Sort32? sort = Models.Sort32.Date,
    Models.Order? order = Models.Order.Asc,
    int? offset = null,
    int? limit = 100)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `accountId` | `Guid` | Template, Required | The unique identifier of the account. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `startDate` | `string` | Query, Optional | Returns account returns starting from and including this date (UTC) |
| `endDate` | `string` | Query, Optional | Returns account returns up until this date (UTC) |
| `sort` | [`Sort32?`](../../doc/models/sort-32.md) | Query, Optional | Sort the result by `date`.<br><br>**Default**: `Sort32.date` |
| `order` | [`Order?`](../../doc/models/order.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. Use `ASC` for ascending or `DESC` for descending sort order.<br><br>**Default**: `Order.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |

## Requires scope

### oauth-client-credentials

`account_returns:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.AccountReturnListResponse](../../doc/models/account-return-list-response.md).

## Example Usage

```csharp
Guid accountId = new Guid("00001114-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
string startDate = "2023-08-15";
string endDate = "2023-08-15";
Sort32? sort = Sort32.Date;
Order? order = Order.Asc;
int? limit = 100;
try
{
    ApiResponse<AccountReturnListResponse> result = await returnsApi.ListAccountReturnsAsync(
        accountId,
        upvestClientId,
        upvestApiVersion,
        startDate,
        endDate,
        sort,
        order,
        null,
        limit
    );
}
catch (ApiException e)
{
    Console.WriteLine(e.Message);
    if (e is ErrorException)
    {
       // TODO: Handle ErrorException exception here
    }
}
```

## Example Response *(as JSON)*

```json
{
  "meta": {
    "offset": 0,
    "limit": 100,
    "count": 2,
    "total_count": 2,
    "sort": "date",
    "order": "ASC"
  },
  "data": [
    {
      "account_id": "bbe9e784-e009-4cb6-a07c-24831897327b",
      "date": "2023-09-01T00:00:00Z",
      "twr": {
        "daily": "0.0132434142",
        "cumulative": "0.0324353234",
        "cumulative_start_date": "2023-08-01T00:00:00Z"
      }
    },
    {
      "account_id": "bbe9e784-e009-4cb6-a07c-24831897327b",
      "date": "2023-09-02T00:00:00Z",
      "twr": {
        "daily": "-0.005116543491",
        "cumulative": "0.0271528232",
        "cumulative_start_date": "2023-08-01T00:00:00Z"
      }
    }
  ]
}
```

## Errors

| HTTP Status Code | Error Description | Exception Class |
|  --- | --- | --- |
| 400 | Bad Request. The incoming request had a malformed parameter/object. | [`ErrorException`](../../doc/models/error-exception.md) |
| 401 | Unauthorized. The caller has not been authenticated. | [`ErrorException`](../../doc/models/error-exception.md) |
| 403 | Forbidden. The caller has been authenticated but is not allowed to take the requested action. | [`ErrorException`](../../doc/models/error-exception.md) |
| 404 | Not Found. The requested resource could not be found. | [`ErrorException`](../../doc/models/error-exception.md) |
| 405 | Method Not Allowed. The requested method is not allowed on the requested resource. | [`ErrorException`](../../doc/models/error-exception.md) |
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |

