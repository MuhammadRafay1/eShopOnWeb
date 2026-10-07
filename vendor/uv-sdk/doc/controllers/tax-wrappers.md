# Tax Wrappers

```csharp
TaxWrappersApi taxWrappersApi = client.TaxWrappersApi;
```

## Class Name

`TaxWrappersApi`

## Methods

* [Create Isa Tax Wrapper](../../doc/controllers/tax-wrappers.md#create-isa-tax-wrapper)
* [Retrieve Isa Allowances](../../doc/controllers/tax-wrappers.md#retrieve-isa-allowances)


# Create Isa Tax Wrapper

Creates a tax wrapper for ISA account.

```csharp
CreateIsaTaxWrapperAsync(
    Guid upvestClientId,
    Guid idempotencyKey,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.TaxWrappersCreateIsaTaxWrapperRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `idempotencyKey` | `Guid` | Header, Required | A UUID to be used as an idempotency key.  This prevents a duplicate request from being replayed.<br>https://docs.upvest.co/documentation/concepts/api_concepts/idempotency |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`TaxWrappersCreateIsaTaxWrapperRequest`](../../doc/models/tax-wrappers-create-isa-tax-wrapper-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`taxes:admin`

## Response Type

**200**: ISA tax wrapper created.

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.TaxWrappersIsaTaxWrapper](../../doc/models/tax-wrappers-isa-tax-wrapper.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid idempotencyKey = new Guid("ccb07f42-4104-44ad-8e1f-c660bb7b269c");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
TaxWrappersCreateIsaTaxWrapperRequest body = new TaxWrappersCreateIsaTaxWrapperRequest
{
    AccountGroupId = new Guid("0d68fea2-66e8-4ea8-b507-276e7a1eb4aa"),
    Type = "STOCKS_AND_SHARES_ISA",
    IsFlexible = true,
};

try
{
    ApiResponse<TaxWrappersIsaTaxWrapper> result = await taxWrappersApi.CreateIsaTaxWrapperAsync(
        upvestClientId,
        idempotencyKey,
        upvestApiVersion,
        body
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
  "id": "f1a57a04-1a89-4dab-ae3a-ff9b2a9377c1",
  "created_at": "2025-10-05T14:48:00.000Z",
  "updated_at": "2025-10-05T14:48:00.000Z",
  "account_group_id": "0d68fea2-66e8-4ea8-b507-276e7a1eb4aa",
  "is_flexible": true,
  "status": "NEW",
  "type": "STOCKS_AND_SHARES_ISA"
}
```

## Errors

| HTTP Status Code | Error Description | Exception Class |
|  --- | --- | --- |
| 400 | Bad Request. The incoming request had a malformed parameter/object. | [`ErrorException`](../../doc/models/error-exception.md) |
| 401 | Unauthorized. The caller has not been authenticated. | [`ErrorException`](../../doc/models/error-exception.md) |
| 403 | Forbidden. The caller has been authenticated but is not allowed to take the requested action. | [`ErrorException`](../../doc/models/error-exception.md) |
| 404 | Not Found. The requested resource could not be found. | [`ErrorException`](../../doc/models/error-exception.md) |
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# Retrieve Isa Allowances

Retrieves ISA allowances

```csharp
RetrieveIsaAllowancesAsync(
    Guid taxWrapperId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    string taxYear = null,
    Models.Sort20? sort = Models.Sort20.CreatedAt,
    Models.Order? order = Models.Order.Asc,
    int? offset = null,
    int? limit = 100)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `taxWrapperId` | `Guid` | Template, Required | The unique identifier of the tax wrapper. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `taxYear` | `string` | Query, Optional | Filter allowances by tax_year YYYY/YYYY<br><br>**Constraints**: *Pattern*: `^[0-9]{4}\/[0-9]{4}$` |
| `sort` | [`Sort20?`](../../doc/models/sort-20.md) | Query, Optional | Sort the result by `created_at` or `valid_from`<br><br>**Default**: `Sort20.created_at` |
| `order` | [`Order?`](../../doc/models/order.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. Use `ASC` for ascending or `DESC` for descending sort order.<br><br>**Default**: `Order.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |

## Requires scope

### oauth-client-credentials

`taxes:admin`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.TaxWrappersRetrieveIsaAllowancesResponse](../../doc/models/tax-wrappers-retrieve-isa-allowances-response.md).

## Example Usage

```csharp
Guid taxWrapperId = new Guid("000000c4-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
Sort20? sort = Sort20.CreatedAt;
Order? order = Order.Asc;
int? limit = 100;
try
{
    ApiResponse<TaxWrappersRetrieveIsaAllowancesResponse> result = await taxWrappersApi.RetrieveIsaAllowancesAsync(
        taxWrapperId,
        upvestClientId,
        upvestApiVersion,
        null,
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
    "count": 1,
    "total_count": 1,
    "sort": "valid_from",
    "order": "DESC"
  },
  "data": [
    {
      "id": "f1a57a04-1a89-4dab-ae3a-ff9b2a9377c1",
      "created_at": "2025-10-05T14:48:00.000Z",
      "updated_at": "2025-10-05T14:48:00.000Z",
      "tax_wrapper_id": "0d68fea2-66e8-4ea8-b507-276e7a1eb4aa",
      "tax_year": "2025/2026",
      "type": "ANNUAL",
      "status": "ACTIVE",
      "currency": "GBP",
      "used_amount": "0.00",
      "remaining_amount": "20000.00",
      "valid_from": "2025-04-06T00:00:00.000Z",
      "valid_to": "2026-04-05T23:59:59.000Z",
      "first_subscription_at": "2025-04-06T00:00:00.000Z"
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

