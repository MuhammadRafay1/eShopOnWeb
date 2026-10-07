# Direct Debits

```csharp
DirectDebitsApi directDebitsApi = client.DirectDebitsApi;
```

## Class Name

`DirectDebitsApi`

## Methods

* [List Direct Debits](../../doc/controllers/direct-debits.md#list-direct-debits)
* [Create Direct Debit](../../doc/controllers/direct-debits.md#create-direct-debit)
* [Retrieve Direct Debit](../../doc/controllers/direct-debits.md#retrieve-direct-debit)


# List Direct Debits

Returns a paginated list of direct debits for the account group specified by its ID.

```csharp
ListDirectDebitsAsync(
    Guid accountGroupId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.Sort4? sort = Models.Sort4.CreatedAt,
    Models.Order? order = Models.Order.Asc,
    int? limit = 100,
    int? offset = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `accountGroupId` | `Guid` | Template, Required | The unique identifier of the account group. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `sort` | [`Sort4?`](../../doc/models/sort-4.md) | Query, Optional | Field of resource to sort by<br><br>**Default**: `Sort4.created_at` |
| `order` | [`Order?`](../../doc/models/order.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. Use `ASC` for ascending or `DESC` for descending sort order.<br><br>**Default**: `Order.ASC` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |

## Requires scope

### oauth-client-credentials

`payments:admin`, `payments:read`

## Response Type

**200**: Direct debits list

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PaymentsDirectDebitsListResponse](../../doc/models/payments-direct-debits-list-response.md).

## Example Usage

```csharp
Guid accountGroupId = new Guid("00002436-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
Sort4? sort = Sort4.Id;
Order? order = Order.Asc;
int? limit = 100;
try
{
    ApiResponse<PaymentsDirectDebitsListResponse> result = await directDebitsApi.ListDirectDebitsAsync(
        accountGroupId,
        upvestClientId,
        upvestApiVersion,
        sort,
        order,
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
    "sort": "id",
    "order": "ASC"
  },
  "data": [
    {
      "id": "1ab4fef9-a097-4c6f-9345-647025d5fde6",
      "created_at": "2020-08-24T14:15:22Z",
      "user_id": "82b49999-1145-4e05-916a-567662daa7bc",
      "account_group_id": "1ea60f56-b67b-61fc-829a-0242ac130003",
      "mandate_id": "730b8719-0bc6-401d-98dd-b2de72afbf05",
      "cash_amount": "200.00",
      "currency": "EUR",
      "remittance_information": "payment for green energy portfolio",
      "status": "NEW",
      "purpose_code": "OTHR"
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
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# Create Direct Debit

Trigger a direct debit.

**Deprecation notice:** The `user_id` field is deprecated, optional, and will be removed in a future version. For business account direct debits, ownership is determined by the mandate linked to the business.

```csharp
CreateDirectDebitAsync(
    Guid upvestClientId,
    Guid idempotencyKey,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.PaymentsDirectDebitCreateRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `idempotencyKey` | `Guid` | Header, Required | A UUID to be used as an idempotency key.  This prevents a duplicate request from being replayed.<br>https://docs.upvest.co/documentation/concepts/api_concepts/idempotency |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`PaymentsDirectDebitCreateRequest`](../../doc/models/payments-direct-debit-create-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`payments:admin`

## Response Type

**200**: Direct debit

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PaymentsDirectDebitsResponse](../../doc/models/payments-direct-debits-response.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid idempotencyKey = new Guid("ccb07f42-4104-44ad-8e1f-c660bb7b269c");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
PaymentsDirectDebitCreateRequest body = new PaymentsDirectDebitCreateRequest
{
    AccountGroupId = new Guid("1ea60f56-b67b-61fc-829a-0242ac130003"),
    MandateId = new Guid("95b8304e-19fc-4cd9-8e50-82532e6b8d8c"),
    CashAmount = "200.00",
    UserId = new Guid("83d83ec2-d2ca-49ff-bbea-b92b5c3be202"),
    Currency = Currency.Eur,
    RemittanceInformation = "payment for green energy portfolio",
    PurposeCode = "OTHR",
};

try
{
    ApiResponse<PaymentsDirectDebitsResponse> result = await directDebitsApi.CreateDirectDebitAsync(
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
  "id": "1ab4fef9-a097-4c6f-9345-647025d5fde6",
  "created_at": "2020-08-24T14:15:22Z",
  "user_id": "82b49999-1145-4e05-916a-567662daa7bc",
  "account_group_id": "1ea60f56-b67b-61fc-829a-0242ac130003",
  "mandate_id": "730b8719-0bc6-401d-98dd-b2de72afbf05",
  "cash_amount": "200.00",
  "currency": "EUR",
  "status": "NEW",
  "purpose_code": "OTHR"
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


# Retrieve Direct Debit

Returns the direct debit specified by its ID, including its current status, cash amount, mandate reference, and any cancellation reason.

```csharp
RetrieveDirectDebitAsync(
    Guid directDebitId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `directDebitId` | `Guid` | Template, Required | The unique identifier of the direct debit. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`payments:admin`, `payments:read`

## Response Type

**200**: Direct debit payment

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PaymentsDirectDebitsResponse](../../doc/models/payments-direct-debits-response.md).

## Example Usage

```csharp
Guid directDebitId = new Guid("00002410-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<PaymentsDirectDebitsResponse> result = await directDebitsApi.RetrieveDirectDebitAsync(
        directDebitId,
        upvestClientId,
        upvestApiVersion
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
  "id": "1ab4fef9-a097-4c6f-9345-647025d5fde6",
  "created_at": "2020-08-24T14:15:22Z",
  "user_id": "82b49999-1145-4e05-916a-567662daa7bc",
  "account_group_id": "1ea60f56-b67b-61fc-829a-0242ac130003",
  "mandate_id": "730b8719-0bc6-401d-98dd-b2de72afbf05",
  "cash_amount": "200.00",
  "currency": "EUR",
  "remittance_information": "payment for green energy portfolio",
  "status": "NEW",
  "purpose_code": "OTHR"
}
```

## Errors

| HTTP Status Code | Error Description | Exception Class |
|  --- | --- | --- |
| 401 | Unauthorized. The caller has not been authenticated. | [`ErrorException`](../../doc/models/error-exception.md) |
| 403 | Forbidden. The caller has been authenticated but is not allowed to take the requested action. | [`ErrorException`](../../doc/models/error-exception.md) |
| 404 | Not Found. The requested resource could not be found. | [`ErrorException`](../../doc/models/error-exception.md) |
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |

